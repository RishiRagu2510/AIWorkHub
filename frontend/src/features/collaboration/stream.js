// Each reconnect establishes a fresh authorized server stream.
export function startCollaborationStream(
  connection,
  method,
  args,
  onChange,
  onState,
  onInterrupted,
) {
  let disposed = false,
    retry,
    subscription;
  function later() {
    if (!disposed && !retry)
      retry = setTimeout(() => {
        retry = null;
        start();
      }, 15000);
  }
  function interrupted() {
    if (disposed) return;
    onState('Reconnecting…');
    onInterrupted();
    void connection
      .stop()
      .catch(() => {})
      .finally(later);
  }
  function subscribe() {
    if (disposed) return;
    subscription?.dispose();
    onState('Connected');
    subscription = connection.stream(method, ...args).subscribe({
      next: (event) => {
        if (!disposed) onChange(event);
      },
      error: interrupted,
      complete: interrupted,
    });
  }
  async function start() {
    try {
      await connection.start();
      if (disposed) await connection.stop();
      else subscribe();
    } catch {
      if (!disposed) {
        onState('Offline · retrying');
        onInterrupted();
        later();
      }
    }
  }
  connection.onreconnecting(() => {
    if (!disposed) {
      onState('Reconnecting…');
      onInterrupted();
    }
  });
  connection.onreconnected(subscribe);
  connection.onclose(() => {
    if (!disposed) {
      onState('Offline · retrying');
      onInterrupted();
      later();
    }
  });
  start();
  return () => {
    disposed = true;
    clearTimeout(retry);
    subscription?.dispose();
    void connection.stop().catch(() => {});
  };
}
