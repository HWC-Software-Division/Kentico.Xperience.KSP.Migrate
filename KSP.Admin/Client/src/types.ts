export interface ContentTypeField {
  name: string; dataType: string; isRequired: boolean; size: number;
  defaultValue: string | null; fieldType: string; caption: string | null;
  dataSource: string | null; minItems: number | null; maxItems: number | null;
  allowedContentTypes: string[] | null; visible: boolean;
}
export interface ContentType { name: string; codeName: string; fields: ContentTypeField[]; }

export interface FieldSchema {
  name: string;
  displayName: string;
  description: string | null;
  guid: string;
  fieldCount: number;
}

export interface ApiResponse<T> { success: boolean; data?: T; error?: string; }

export interface ImportResult {
  created: number; updated: number; errors: string[];
  createdNames: string[]; updatedNames: string[];
  reusableCreated: number; reusableUpdated: number; reusableErrors: string[];
  reusableCreatedNames: string[]; reusableUpdatedNames: string[];
  schemaCreated: number; schemaUpdated: number; schemaErrors: string[];
  schemaCreatedNames: string[]; schemaUpdatedNames: string[];
}

export interface BasePageProps { apiBaseUrl: string; }
