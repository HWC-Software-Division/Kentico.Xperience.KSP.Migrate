using CMS.ContentEngine;
using CMS.DataEngine;
using Kentico.Xperience.KSP.Migrate.Models.API;
using System;
using System.Collections.Generic;
using System.Linq;
using XperienceCommunity.Localization;
using static Kentico.Xperience.KSP.Migrate.Services.LocalStringMigrationService;

namespace Kentico.Xperience.KSP.Migrate.Services
{
    public interface ILocalStringMigrationService
    {
        LocalStringBatchImportResult ImportMany(IEnumerable<LocalStringImportDto> models);
    }

    public class LocalStringMigrationService : ILocalStringMigrationService
    {
        private readonly IInfoProvider<LocalizationKeyInfo> localizationKeyProvider;
        private readonly IInfoProvider<LocalizationTranslationItemInfo> localizationTranslationProvider;
        private readonly IInfoProvider<ContentLanguageInfo> contentLanguageProvider;

        public LocalStringMigrationService(
            IInfoProvider<LocalizationKeyInfo> localizationKeyProvider,
            IInfoProvider<LocalizationTranslationItemInfo> localizationTranslationProvider,
            IInfoProvider<ContentLanguageInfo> contentLanguageProvider)
        {
            this.localizationKeyProvider = localizationKeyProvider;
            this.localizationTranslationProvider = localizationTranslationProvider;
            this.contentLanguageProvider = contentLanguageProvider;
        }

        public LocalStringBatchImportResult ImportMany(IEnumerable<LocalStringImportDto> models)
        {
            var result = new LocalStringBatchImportResult();

            if (models == null)
            {
                result.Errors.Add(new LocalStringImportError
                {
                    Key = string.Empty,
                    Message = "Request body is empty."
                });

                return result;
            }

            foreach (var model in models)
            {
                try
                {
                    if (model == null)
                    {
                        result.Errors.Add(new LocalStringImportError
                        {
                            Key = string.Empty,
                            Message = "One item in request body is null."
                        });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(model.Key))
                    {
                        result.Errors.Add(new LocalStringImportError
                        {
                            Key = string.Empty,
                            Message = "Key is required."
                        });
                        continue;
                    }

                    var itemResult = ImportSingle(model);

                    result.Items.Add(itemResult);
                    result.TotalKeysProcessed++;
                    result.TotalTranslationsProcessed += itemResult.TranslationsProcessed;
                    result.TotalTranslationsCreated += itemResult.TranslationsCreated;
                    result.TotalTranslationsUpdated += itemResult.TranslationsUpdated;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new LocalStringImportError
                    {
                        Key = model?.Key ?? string.Empty,
                        Message = ex.Message
                    });
                }
            }

            return result;
        }

        private LocalStringImportResult ImportSingle(LocalStringImportDto model)
        {
            var result = new LocalStringImportResult
            {
                Key = model.Key
            };

            var keyItem = localizationKeyProvider.Get()
                .WhereEquals(nameof(LocalizationKeyInfo.LocalizationKeyItemName), model.Key)
                .TopN(1)
                .FirstOrDefault();

            var isNewKey = keyItem == null;

            if (keyItem == null)
            {
                keyItem = new LocalizationKeyInfo
                {
                    LocalizationKeyItemGuid = Guid.NewGuid(),
                    LocalizationKeyItemName = model.Key,
                    LocalizationKeyItemDescription = model.Description ?? string.Empty
                };
            }
            else
            {
                keyItem.LocalizationKeyItemDescription = model.Description ?? keyItem.LocalizationKeyItemDescription;
            }

            localizationKeyProvider.Set(keyItem);

            result.KeyItemId = keyItem.LocalizationKeyItemId;
            result.KeyCreated = isNewKey;

            foreach (var translation in model.Values ?? Enumerable.Empty<LocalStringTranslationDto>())
            {
                if (string.IsNullOrWhiteSpace(translation.Language))
                {
                    result.Warnings.Add("Skipped one translation because language is empty.");
                    continue;
                }

                var language = ResolveContentLanguage(translation.Language);

                if (language == null)
                {
                    result.Warnings.Add($"Language '{translation.Language}' not found.");
                    continue;
                }

                var translationItem = localizationTranslationProvider.Get()
                    .WhereEquals(
                        nameof(LocalizationTranslationItemInfo.LocalizationTranslationItemLocalizationKeyItemId),
                        keyItem.LocalizationKeyItemId)
                    .WhereEquals(
                        nameof(LocalizationTranslationItemInfo.LocalizationTranslationItemContentLanguageId),
                        language.ContentLanguageID)
                    .TopN(1)
                    .FirstOrDefault();

                var isNewTranslation = translationItem == null;

                if (translationItem == null)
                {
                    translationItem = new LocalizationTranslationItemInfo
                    {
                        LocalizationTranslationItemGuid = Guid.NewGuid(),
                        LocalizationTranslationItemLocalizationKeyItemId = keyItem.LocalizationKeyItemId,
                        LocalizationTranslationItemContentLanguageId = language.ContentLanguageID,
                        LocalizationTranslationItemText = translation.Value ?? string.Empty
                    };
                }
                else
                {
                    translationItem.LocalizationTranslationItemText = translation.Value ?? string.Empty;
                }

                localizationTranslationProvider.Set(translationItem);

                result.TranslationsProcessed++;

                if (isNewTranslation)
                {
                    result.TranslationsCreated++;
                }
                else
                {
                    result.TranslationsUpdated++;
                }
            }

            return result;
        }

        private ContentLanguageInfo? ResolveContentLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return null;
            }

            var normalized = languageCode.Trim().Replace("_", "-");

            var allLanguages = contentLanguageProvider.Get().ToList();

            // 1) exact match ก่อน
            var exact = allLanguages.FirstOrDefault(x =>
                string.Equals(x.ContentLanguageName, normalized, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                return exact;
            }

            // 2) ถ้าส่ง en-US มา แต่ใน DB มีแค่ en
            var neutralCode = normalized.Split('-')[0];

            var neutralMatch = allLanguages.FirstOrDefault(x =>
                string.Equals(x.ContentLanguageName, neutralCode, StringComparison.OrdinalIgnoreCase));

            if (neutralMatch != null)
            {
                return neutralMatch;
            }

            // 3) เผื่อกลับกัน: ถ้าส่ง en แต่ใน DB เก็บ en-US
            var regionalMatch = allLanguages.FirstOrDefault(x =>
                x.ContentLanguageName.StartsWith(neutralCode + "-", StringComparison.OrdinalIgnoreCase));

            return regionalMatch;
        }

        public class LocalStringImportResult
        {
            public string Key { get; set; } = string.Empty;
            public int KeyItemId { get; set; }
            public bool KeyCreated { get; set; }
            public int TranslationsProcessed { get; set; }
            public int TranslationsCreated { get; set; }
            public int TranslationsUpdated { get; set; }
            public List<string> Warnings { get; set; } = new();
        }

        public class LocalStringBatchImportResult
        {
            public int TotalKeysProcessed { get; set; }
            public int TotalTranslationsProcessed { get; set; }
            public int TotalTranslationsCreated { get; set; }
            public int TotalTranslationsUpdated { get; set; }
            public List<LocalStringImportResult> Items { get; set; } = new();
            public List<LocalStringImportError> Errors { get; set; } = new();
        }

        public class LocalStringImportError
        {
            public string Key { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }
    }
}