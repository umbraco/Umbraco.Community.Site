/* aligned with marketplace type, may not be using all properties */
export interface DcFilterModel {
    label: string;
    alias: string;
    controlType: 'select' | 'radio' | 'checkbox';
    options: Array<Option>;
    tooltip?: string;
    active?: boolean;
}