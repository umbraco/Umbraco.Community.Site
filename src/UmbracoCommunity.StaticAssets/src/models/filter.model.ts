type FilterModel = {
  label: string;
  alias: string;
  controlType: "select" | "radio" | "checkbox" | "text" | "checkboxlist" | "dropdown";
  options?: Array<Option>;
  tooltip?: string;
  active?: boolean;
  defaultValue?: string | null;
  value?: string | Array<string>;
  sortOrder?: Array<string>;
};
