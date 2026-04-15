import { Injectable, NgZone } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';

export interface TransactionSignedEvent {
  transactionId: number;
  signatureCount: number;
  requiredSignatures: number;
  timestamp: string;
}

export interface TransactionFinalizedEvent {
  transactionId: number;
  fee: number;
  settlement: number;
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: signalR.HubConnection | null = null;
  private readonly hubUrl = 'http://localhost:5000/hubs/transactions';

  public transactionSigned$ = new Subject<TransactionSignedEvent>();
  public transactionFinalized$ = new Subject<TransactionFinalizedEvent>();
  public connectionStatus$ = new Subject<boolean>();

  constructor(private ngZone: NgZone) {}

  start(): void {
    if (this.hubConnection) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.hubConnection.onreconnecting(() => {
      console.log('[SignalR] Reconnecting...');
      this.ngZone.run(() => this.connectionStatus$.next(false));
    });

    this.hubConnection.onreconnected(() => {
      console.log('[SignalR] Reconnected');
      this.ngZone.run(() => this.connectionStatus$.next(true));
    });

    this.hubConnection.onclose(() => {
      console.log('[SignalR] Connection closed');
      this.ngZone.run(() => this.connectionStatus$.next(false));
    });

    this.hubConnection.on('TransactionSigned', (event: TransactionSignedEvent) => {
      console.log('[SignalR] TransactionSigned:', event);
      this.ngZone.run(() => this.transactionSigned$.next(event));
    });

    this.hubConnection.on('TransactionFinalized', (event: TransactionFinalizedEvent) => {
      console.log('[SignalR] TransactionFinalized:', event);
      this.ngZone.run(() => this.transactionFinalized$.next(event));
    });

    this.hubConnection
      .start()
      .then(() => {
        console.log('[SignalR] Connected');
        this.ngZone.run(() => this.connectionStatus$.next(true));
      })
      .catch(err => {
        console.error('[SignalR] Connection error:', err);
        this.ngZone.run(() => this.connectionStatus$.next(false));
      });
  }

  stop(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.hubConnection = null;
    }
  }
}
