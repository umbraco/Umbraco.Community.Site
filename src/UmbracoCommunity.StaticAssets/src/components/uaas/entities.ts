export interface PurchaseFlowArgs {
  plan: string | null;
  sku: string | null;
  code: string | null;
  planTitle: string | null;
}

export interface PurchaseFlowLog {
  reason?: string | null;
  description?: string | null;
}

export interface PurchaseFlowForm {
  name?: string | null;
  email?: string | null;
  password?: string | null;
  consent: boolean;
}
