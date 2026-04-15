/**
 * k6 Load Test - Transaction Signing Workflow
 *
 * Runs until all transactions are signed (no more signable transactions).
 *
 * Run: k6 run k6-workflow.js --env BASE_URL=http://localhost:5000
 * With restore cycles: k6 run k6-workflow.js --env BASE_URL=http://localhost:5000 --env RESTORE=1
 */

import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Trend } from "k6/metrics";
import exec from "k6/execution";

// Metrics
const failures = new Counter("workflow_failures");
const successfulSigns = new Counter("successful_signs");
const signingDuration = new Trend("signing_duration_ms");

// Configuration from environment
const BASE_URL = __ENV.BASE_URL || "http://localhost:5000";
const RESTORE_MODE = __ENV.RESTORE === "1"; // If true, restore and continue; if false, stop when done
const USER_POOL = Number(__ENV.USER_POOL || 10);
const MAX_DURATION = __ENV.MAX_DURATION || "30m"; // Safety limit

// Timing
const POLL_SEC = 2;
const MIN_DELAY = 0.1;
const MAX_DELAY = 0.5;

// Shared state to signal completion
let allSigned = false;

export const options = {
  scenarios: {
    signers: {
      executor: "constant-vus",
      exec: "signerFlow",
      vus: 10,
      duration: MAX_DURATION,
      gracefulStop: "5s",
    },
    monitor: {
      executor: "constant-vus",
      exec: "monitorCompletion",
      vus: 1,
      duration: MAX_DURATION,
      gracefulStop: "0s",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.10"],
  },
};

// Helpers
const randomDelay = () => sleep(MIN_DELAY + Math.random() * (MAX_DELAY - MIN_DELAY));
const api = {
  pending: () => `${BASE_URL}/api/transactions/pending`,
  sign: () => `${BASE_URL}/api/transactions/sign`,
  restore: () => `${BASE_URL}/api/transactions/restore-all`,
};

function logFail(kind, data) {
  failures.add(1);
  console.log(JSON.stringify({ level: "fail", ts: new Date().toISOString(), kind, ...data }));
}

function getPending(userId) {
  const res = http.get(api.pending(), {
    headers: { "X-User-Id": String(userId) },
    tags: { name: "GET /pending" },
  });

  if (!check(res, { "pending 200": (r) => r.status === 200 })) {
    logFail("pending_error", { userId, status: res.status });
    return [];
  }

  try {
    return res.json() || [];
  } catch {
    return [];
  }
}

function hasAnySignableTransactions() {
  for (let u = 1; u <= USER_POOL; u++) {
    const pending = getPending(u);
    if (pending.some((t) => t?.canSign === true)) {
      return true;
    }
  }
  return false;
}

// Main signer flow
export function signerFlow() {
  // Check if we should stop
  if (allSigned) {
    sleep(1);
    return;
  }

  const userId = (__VU % USER_POOL) + 1;
  randomDelay();

  const pending = getPending(userId);
  const signable = pending.filter((t) => t?.canSign === true);

  if (signable.length === 0) {
    sleep(0.5); // Brief wait before retry
    return;
  }

  const tx = signable[Math.floor(Math.random() * signable.length)];
  randomDelay();

  const startTime = Date.now();
  const res = http.post(
    api.sign(),
    JSON.stringify({ transactionIds: [tx.id] }),
    {
      headers: {
        "Content-Type": "application/json",
        "X-User-Id": String(userId),
      },
      tags: { name: "POST /sign" },
    }
  );
  signingDuration.add(Date.now() - startTime);

  if (!check(res, { "sign 200": (r) => r.status === 200 })) {
    logFail("sign_error", { userId, txId: tx.id, status: res.status });
    return;
  }

  try {
    const result = res.json()?.[0];
    if (result?.success) {
      successfulSigns.add(1);
    } else {
      logFail("sign_fail", { userId, txId: tx.id, error: result?.error });
    }
  } catch {
    logFail("sign_parse", { userId, txId: tx.id });
  }
}

// Monitor for completion
export function monitorCompletion() {
  while (true) {
    sleep(POLL_SEC);

    const hasSignable = hasAnySignableTransactions();

    if (!hasSignable) {
      if (RESTORE_MODE) {
        // Restore and continue
        console.log(JSON.stringify({ level: "info", action: "restore", ts: new Date().toISOString() }));
        const res = http.post(api.restore(), null, { tags: { name: "POST /restore" } });
        check(res, { "restore 200": (r) => r.status === 200 });
      } else {
        // Signal completion and stop
        console.log(JSON.stringify({
          level: "info",
          action: "complete",
          message: "All transactions signed",
          ts: new Date().toISOString()
        }));
        allSigned = true;

        // Give signers time to notice and wind down
        sleep(3);

        // Abort the test
        exec.test.abort("All transactions signed - test complete");
      }
    }
  }
}
