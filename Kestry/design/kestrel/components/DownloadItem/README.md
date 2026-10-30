# DownloadItem

One row in the download list: file tile, name, status, progress, details and actions. Pass raw numbers (bytes, bytes/s, seconds); the row formats them. Actions follow the status: downloading → Pause, Cancel; paused → Resume, Cancel; queued → Cancel; failed → Retry, Remove; completed → Show in folder, Remove. Stack rows with `space-2` between. The consumer provides the handlers and the `fileType`.
