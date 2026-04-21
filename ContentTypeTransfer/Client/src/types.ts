// ── DTOs — mirror C# ContentTypeDto / ContentTypeFieldDto ────────────────

export interface ContentTypeField {
  name: string;
  dataType: string;
  isRequired: boolean;
  size: number;
  defaultValue: string | null;
  fieldType: string;
  caption: string | null;
  dataSource: string | null;
  minItems: number | null;
  maxItems: number | null;
  allowedContentTypes: string[] | null;
  visible: boolean;
}

export interface ContentType {
  name: string;
  codeName: string;
  fields: ContentTypeField[];
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

export interface ImportResult {
  created: number;
  updated: number;
  errors: string[];
}

// Props injected by back-end Page<TClientProperties>.ConfigureTemplateProperties
export interface BasePageProps {
  apiBaseUrl: string;
}
