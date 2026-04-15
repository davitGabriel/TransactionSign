import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { TransactionService } from './services/transaction.service';
import { SignalRService, TransactionSignedEvent, TransactionFinalizedEvent } from './services/signalr.service';
import { Transaction, CompletedTransaction } from './models/transaction.model';

interface Toast {
  id: number;
  message: string;
  type: 'info' | 'success' | 'warning';
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
  activeTab = 'pending';
  pendingTransactions: Transaction[] = [];
  completedTransactions: CompletedTransaction[] = [];
  selectedIds: Set<number> = new Set();
  userId = 1;
  toasts: Toast[] = [];
  private toastId = 0;
  private subscriptions: Subscription[] = [];
  private visibilityHandler = () => this.onVisibilityChange();

  constructor(
    private transactionService: TransactionService,
    private signalRService: SignalRService
  ) {}

  ngOnInit(): void {
    this.loadPendingTransactions();
    this.loadCompletedTransactions();
    this.setupSignalR();
    this.setupVisibilityHandler();
  }

  private setupVisibilityHandler(): void {
    document.addEventListener('visibilitychange', this.visibilityHandler);
  }

  private onVisibilityChange(): void {
    if (document.visibilityState === 'visible') {
      // Refresh data when user returns to the tab
      this.loadPendingTransactions();
      this.loadCompletedTransactions();
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.signalRService.stop();
    document.removeEventListener('visibilitychange', this.visibilityHandler);
  }

  private setupSignalR(): void {
    this.signalRService.start();

    // Subscribe to signature events
    this.subscriptions.push(
      this.signalRService.transactionSigned$.subscribe(event => {
        this.handleTransactionSigned(event);
      })
    );

    // Subscribe to finalization events
    this.subscriptions.push(
      this.signalRService.transactionFinalized$.subscribe(event => {
        this.handleTransactionFinalized(event);
      })
    );
  }

  private handleTransactionSigned(event: TransactionSignedEvent): void {
    // Update the transaction in the list
    const txn = this.pendingTransactions.find(t => t.id === event.transactionId);
    if (txn) {
      txn.signatureCount = event.signatureCount;

      // If signature count reached required, disable signing and notify user
      if (event.signatureCount >= event.requiredSignatures) {
        txn.canSign = false;
        // Remove from selection if it was selected
        this.selectedIds.delete(event.transactionId);

        this.showToast(
          `Transaction #${event.transactionId} has been fully signed (${event.signatureCount}/${event.requiredSignatures}) and will be completed shortly.`,
          'info'
        );
      }
    }
  }

  private handleTransactionFinalized(event: TransactionFinalizedEvent): void {
    // Remove from pending list and show notification
    const index = this.pendingTransactions.findIndex(t => t.id === event.transactionId);
    if (index >= 0) {
      this.pendingTransactions.splice(index, 1);
      this.selectedIds.delete(event.transactionId);
    }

    this.showToast(
      `Transaction #${event.transactionId} has been finalized!`,
      'success'
    );

    // Refresh completed transactions
    this.loadCompletedTransactions();
  }

  private showToast(message: string, type: 'info' | 'success' | 'warning'): void {
    const toast: Toast = { id: ++this.toastId, message, type };
    this.toasts.push(toast);

    // Auto-remove after 8 seconds
    setTimeout(() => {
      this.removeToast(toast.id);
    },8000);
  }

  removeToast(id: number): void {
    this.toasts = this.toasts.filter(t => t.id !== id);
  }

  setUserId(): void {
    this.transactionService.setUserId(this.userId);
    this.loadPendingTransactions();
  }

  loadPendingTransactions(): void {
    this.transactionService.getPendingTransactions().subscribe({
      next: (data) => {
        this.pendingTransactions = data;
        this.selectedIds.clear();
      },
      error: (err) => console.error('Error loading pending:', err)
    });
  }

  loadCompletedTransactions(): void {
    this.transactionService.getCompletedTransactions().subscribe({
      next: (data) => this.completedTransactions = data,
      error: (err) => console.error('Error loading completed:', err)
    });
  }

  switchTab(tab: string): void {
    this.activeTab = tab;
    if (tab === 'pending') {
      this.loadPendingTransactions();
    } else {
      this.loadCompletedTransactions();
    }
  }

  toggleSelection(id: number): void {
    if (this.selectedIds.has(id)) {
      this.selectedIds.delete(id);
    } else {
      this.selectedIds.add(id);
    }
  }

  isSelected(id: number): boolean {
    return this.selectedIds.has(id);
  }

  get hasSelection(): boolean {
    return this.selectedIds.size > 0;
  }

  signSelected(): void {
    if (!this.hasSelection) return;

    const ids = Array.from(this.selectedIds);
    this.transactionService.signTransactions(ids).subscribe({
      next: () => {
        this.loadPendingTransactions();
        this.loadCompletedTransactions();
      },
      error: (err) => console.error('Error signing:', err)
    });
  }

  restoreTransaction(id: number): void {
    this.transactionService.restoreTransaction(id).subscribe({
      next: () => {
        this.loadPendingTransactions();
        this.loadCompletedTransactions();
      },
      error: (err) => console.error('Error restoring:', err)
    });
  }

  restoreAllTransactions(): void {
    this.transactionService.restoreAllTransactions().subscribe({
      next: () => {
        this.loadPendingTransactions();
        this.loadCompletedTransactions();
      },
      error: (err) => console.error('Error restoring all:', err)
    });
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString();
  }
}
