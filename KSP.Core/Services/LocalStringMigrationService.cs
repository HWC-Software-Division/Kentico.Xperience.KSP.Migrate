using CMS.ContentEngine;
using CMS.DataEngine;
using KSP.Core.Models;
using XperienceCommunity.Localization;

namespace KSP.Core.Services;

public class LocalStringMigrationService : ILocalStringMigrationService
{
    private readonly IInfoProvider<LocalizationKeyInfo> _keyProvider;
    private readonly IInfoProvider<LocalizationTranslationItemInfo> _translationProvider;
    private readonly IInfoProvider<ContentLanguageInfo> _languageProvider;

    public LocalStringMigrationService(
        IInfoProvider<LocalizationKeyInfo> keyProvider,
        IInfoProvider<LocalizationTranslationItemInfo> translationProvider,
        IInfoProvider<ContentLanguageInfo> languageProvider)
    {
        _keyProvider         = keyProvider;
        _translationProvider = translationProvider;
        _languageProvider    = languageProvider;
    }

    public LocalStringBatchImportResult ImportMany(IEnumerable<LocalStringImportDto> models)
    {
        var result = new LocalStringBatchImportResult();

        foreach (var model in models ?? Enumerable.Empty<LocalStringImportDto>())
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model?.Key))
                {
                    result.Errors.Add(new LocalStringImportError { Message = "Key is required." });
                    continue;
                }

                var item = ImportSingle(model);
                result.Items.Add(item);
                result.TotalKeysProcessed++;
                result.TotalTranslationsProcessed += item.TranslationsProcessed;
                result.TotalTranslationsCreated   += item.TranslationsCreated;
                result.TotalTranslationsUpdated   += item.TranslationsUpdated;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new LocalStringImportError
                {
                    Key     = model?.Key ?? string.Empty,
                    Message = ex.Message
                });
            }
        }

        return result;
    }

    private LocalStringImportResult ImportSingle(LocalStringImportDto model)
    {
        var result = new LocalStringImportResult { Key = model.Key };

        var keyItem = _keyProvider.Get()
            .WhereEquals(nameof(LocalizationKeyInfo.LocalizationKeyItemName), model.Key)
            .TopN(1).FirstOrDefault();

        var isNew = keyItem == null;

        if (keyItem == null)
        {
            keyItem = new LocalizationKeyInfo
            {
                LocalizationKeyItemGuid        = Guid.NewGuid(),
                LocalizationKeyItemName        = model.Key,
                LocalizationKeyItemDescription = model.Description ?? string.Empty
            };
        }
        else
        {
            keyItem.LocalizationKeyItemDescription = model.Description ?? keyItem.LocalizationKeyItemDescription;
        }

        _keyProvider.Set(keyItem);
        result.KeyItemId  = keyItem.LocalizationKeyItemId;
        result.KeyCreated = isNew;

        foreach (var t in model.Values ?? Enumerable.Empty<LocalStringTranslationDto>())
        {
            var lang = ResolveLanguage(t.Language);
            if (lang == null) { result.Warnings.Add($"Language '{t.Language}' not found."); continue; }

            var trans = _translationProvider.Get()
                .WhereEquals(nameof(LocalizationTranslationItemInfo.LocalizationTranslationItemLocalizationKeyItemId), keyItem.LocalizationKeyItemId)
                .WhereEquals(nameof(LocalizationTranslationItemInfo.LocalizationTranslationItemContentLanguageId), lang.ContentLanguageID)
                .TopN(1).FirstOrDefault();

            var isNewTrans = trans == null;

            if (trans == null)
            {
                trans = new LocalizationTranslationItemInfo
                {
                    LocalizationTranslationItemGuid                      = Guid.NewGuid(),
                    LocalizationTranslationItemLocalizationKeyItemId     = keyItem.LocalizationKeyItemId,
                    LocalizationTranslationItemContentLanguageId         = lang.ContentLanguageID,
                    LocalizationTranslationItemText                      = t.Value ?? string.Empty
                };
            }
            else { trans.LocalizationTranslationItemText = t.Value ?? string.Empty; }

            _translationProvider.Set(trans);
            result.TranslationsProcessed++;
            if (isNewTrans) result.TranslationsCreated++; else result.TranslationsUpdated++;
        }

        return result;
    }

    private ContentLanguageInfo? ResolveLanguage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var norm = code.Trim().Replace("_", "-");
        var all  = _languageProvider.Get().ToList();
        return all.FirstOrDefault(x => string.Equals(x.ContentLanguageName, norm, StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(x => string.Equals(x.ContentLanguageName, norm.Split('-')[0], StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(x => x.ContentLanguageName.StartsWith(norm.Split('-')[0] + "-", StringComparison.OrdinalIgnoreCase));
    }
}
