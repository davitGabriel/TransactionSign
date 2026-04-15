export interface Transaction {
  id: number;
  type: string;
  valueDate: string;
  lastModifiedDate: string;
  reason: string | null;
  company: string;
  counterparty: string;
  amount: number;
  status: number;
  internalStatus: number;
  signatureCount: number;
  requiredSignatures: number;
  canSign: boolean;
}

export interface CompletedTransaction {
  id: number;
  amount: number;
  fee: number;
  settlementAmount: number;
  status: string;
}

export interface SignResult {
  transactionId: number;
  success: boolean;
  error?: string;
}
