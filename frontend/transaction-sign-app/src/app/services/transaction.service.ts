import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Transaction, CompletedTransaction, SignResult } from '../models/transaction.model';

@Injectable({
  providedIn: 'root'
})
export class TransactionService {
  private apiUrl = 'http://localhost:5000/api/transactions';
  private userId = 1;

  constructor(private http: HttpClient) {}

  setUserId(userId: number): void {
    this.userId = userId;
  }

  private getHeaders(): HttpHeaders {
    return new HttpHeaders().set('X-User-Id', this.userId.toString());
  }

  getPendingTransactions(): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`${this.apiUrl}/pending`, {
      headers: this.getHeaders()
    });
  }

  getCompletedTransactions(): Observable<CompletedTransaction[]> {
    return this.http.get<CompletedTransaction[]>(`${this.apiUrl}/completed`);
  }

  signTransactions(transactionIds: number[]): Observable<SignResult[]> {
    return this.http.post<SignResult[]>(`${this.apiUrl}/sign`,
      { transactionIds },
      { headers: this.getHeaders() }
    );
  }

  restoreTransaction(transactionId: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/restore`,
      { transactionId },
      { headers: this.getHeaders() }
    );
  }

  restoreAllTransactions(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/restore-all`, {});
  }
}
