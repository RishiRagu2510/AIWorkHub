// Live messages are invalidation hints only. REST remains authoritative.
export function startInboxStream(connection, onChange, onState) {
  let stopped = false;
  let timer;
  let subscription;
  function later() {
    if (stopped || timer) return;
    onState('Reconnecting; automatic refresh remains active.');
    timer = setTimeout(() => {
      timer = null;
      start();
    }, 15000);
  }
  function subscribe() {
    if (stopped) return;
    subscription?.dispose();
    onState('Live notifications connected');
    subscription = connection.stream('Watch').subscribe({
      next: (event) => {
        if (!stopped && (event?.changed === true || event?.issuesChanged === true)) onChange(event);
      },
      error: () => {
        if (!stopped) {
          onState('Live notifications interrupted.');
          connection
            .stop()
            .catch(() => {})
            .finally(later);
        }
      },
      complete: () => {
        if (!stopped)
          connection
            .stop()
            .catch(() => {})
            .finally(later);
      },
    });
  }
  async function start() {
    if (stopped) return;
    try {
      await connection.start();
      if (stopped) await connection.stop();
      else subscribe();
    } catch {
      later();
    }
  }
  connection.onreconnecting(() => {
    if (!stopped) onState('Reconnecting; automatic refresh remains active.');
  });
  connection.onreconnected(subscribe);
  connection.onclose(later);
  start();
  return () => {
    stopped = true;
    clearTimeout(timer);
    subscription?.dispose();
    void connection.stop().catch(() => {});
  };
}
