interface Authenticate {
  sharedSecret: string;
}

interface CreateSubscription {
  id: string;
  path: string;
  context: string;
}

interface WatchQuery {
  subscriptionId: string;
  id: string;
  table: string;
  conditions: any[];
}

interface QueryWatched {
  id: string;
}

interface SubscriptionInvalidated {
  subscriptionId: string;
}
