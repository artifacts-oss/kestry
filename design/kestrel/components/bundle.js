/* @ds-bundle: {"format":4,"namespace":"Kestrel","components":[{"name":"Icon"},{"name":"Button"},{"name":"IconButton"},{"name":"TextField"},{"name":"Select"},{"name":"Checkbox"},{"name":"Switch"},{"name":"StatusBadge"},{"name":"ProgressBar"},{"name":"DownloadItem"},{"name":"NavItem"},{"name":"Toolbar"},{"name":"SpeedStat"},{"name":"Dialog"},{"name":"Toast"},{"name":"EmptyState"}]} */
(function () {
  var R = window.React, h = R.createElement;
  function cx() { return Array.prototype.filter.call(arguments, Boolean).join(" "); }

  var PATHS = {
    download: "M12 3v12M7 10l5 5 5-5M5 21h14",
    pause: "M8 5v14M16 5v14",
    play: "M7 4l13 8-13 8z",
    x: "M6 6l12 12M18 6L6 18",
    plus: "M12 5v14M5 12h14",
    check: "M5 12l5 5 9-10",
    retry: "M4 12a8 8 0 1 0 2.3-5.6M4 4v4h4",
    folder: "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
    search: "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4",
    alert: "M12 9v4M12 16.5v.01M10.3 3.9L2.6 17a2 2 0 0 0 1.7 3h15.4a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z",
    info: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 11v5M12 8v.01",
    file: "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5",
    video: "M4 6h11v12H4zM15 10l5-3v10l-5-3z",
    archive: "M4 4h16v4H4zM5 8v11h14V8M10 12h4",
    music: "M9 18V5l11-2v13M9 18a3 3 0 1 1-6 0 3 3 0 0 1 6 0zM20 16a3 3 0 1 1-6 0 3 3 0 0 1 6 0z",
    image: "M4 4h16v16H4zM4 16l5-5 4 4 3-3 4 4M15 9v.01",
    trash: "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3",
    link: "M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1",
    clock: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 7v5l3 2",
    inbox: "M3 13h5l1 3h6l1-3h5M5 5h14l2 8v6H3v-6z",
    sliders: "M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M14 4v4M8 10v4M16 16v4",
    chevronDown: "M6 9l6 6 6-6",
    gauge: "M4 18a8 8 0 1 1 16 0M12 18l4-6"
  };

  function Icon(p) {
    var size = p.size || 20;
    return h("svg", { className: cx("ks-icon", p.className), width: size, height: size, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: 1.75, strokeLinecap: "round", strokeLinejoin: "round", "aria-hidden": p.label ? undefined : true, role: p.label ? "img" : undefined, "aria-label": p.label },
      h("path", { d: PATHS[p.name] || PATHS.file }));
  }

  function omit(p, keys) { var o = {}; for (var k in p) if (keys.indexOf(k) < 0) o[k] = p[k]; return o; }

  function Button(p) {
    var rest = omit(p, ["variant", "size", "icon", "className", "children"]);
    return h("button", Object.assign({ type: "button" }, rest, { className: cx("ks-btn", "ks-btn-" + (p.variant || "secondary"), p.size === "sm" && "ks-btn-sm", p.className) }),
      p.icon && h(Icon, { name: p.icon, size: p.size === "sm" ? 16 : 18 }), p.children);
  }

  function IconButton(p) {
    var rest = omit(p, ["icon", "label", "variant", "size", "className"]);
    return h("button", Object.assign({ type: "button" }, rest, { "aria-label": p.label, title: p.label, className: cx("ks-iconbtn", p.variant === "secondary" && "ks-iconbtn-secondary", p.size === "sm" && "ks-iconbtn-sm", p.className) }),
      h(Icon, { name: p.icon, size: p.size === "sm" ? 16 : 20 }));
  }

  function describedBy(id, hint, error) { return [hint && id + "-hint", error && id + "-err"].filter(Boolean).join(" ") || undefined; }

  function TextField(p) {
    var id = R.useId();
    var rest = omit(p, ["label", "hint", "error", "icon", "mono", "className"]);
    return h("div", { className: cx("ks-field", p.mono && "ks-mono", p.error && "ks-invalid", p.className) },
      p.label && h("label", { className: "ks-label", htmlFor: id }, p.label),
      h("div", { className: cx("ks-control", p.icon && "ks-control-lead") },
        p.icon && h(Icon, { name: p.icon, size: 18 }),
        h("input", Object.assign({ type: "text" }, rest, { id: id, "aria-invalid": p.error ? true : undefined, "aria-describedby": describedBy(id, p.hint, p.error) }))),
      p.error ? h("span", { className: "ks-error", id: id + "-err" }, p.error) : p.hint && h("span", { className: "ks-hint", id: id + "-hint" }, p.hint));
  }

  function Select(p) {
    var id = R.useId();
    var rest = omit(p, ["label", "hint", "options", "className"]);
    return h("div", { className: cx("ks-select", p.className) },
      p.label && h("label", { className: "ks-label", htmlFor: id }, p.label),
      h("div", { className: "ks-control" },
        h("select", Object.assign({}, rest, { id: id, "aria-describedby": p.hint ? id + "-hint" : undefined }),
          (p.options || []).map(function (o) { o = typeof o === "string" ? { value: o, label: o } : o; return h("option", { key: o.value, value: o.value }, o.label); })),
        h(Icon, { name: "chevronDown", size: 18 })),
      p.hint && h("span", { className: "ks-hint", id: id + "-hint" }, p.hint));
  }

  function Checkbox(p) {
    var rest = omit(p, ["label", "className"]);
    return h("label", { className: cx("ks-check", p.className) }, h("input", Object.assign({ type: "checkbox" }, rest)), p.label);
  }

  function Switch(p) {
    var id = R.useId();
    return h("span", { className: cx("ks-switchwrap", p.className) },
      h("button", { type: "button", role: "switch", id: id, className: "ks-switch", "aria-checked": !!p.checked, disabled: p.disabled,
        onClick: function () { p.onChange && p.onChange(!p.checked); } }),
      p.label && h("label", { htmlFor: id }, p.label));
  }

  var STATUS_LABEL = { downloading: "Downloading", queued: "Queued", paused: "Paused", completed: "Completed", failed: "Failed" };

  function StatusBadge(p) {
    var s = p.status || "queued";
    return h("span", { className: "ks-badge ks-badge-" + s }, p.children || STATUS_LABEL[s]);
  }

  function ProgressBar(p) {
    var s = p.status || "downloading", ind = p.value == null;
    var v = ind ? 0 : Math.max(0, Math.min(100, p.value));
    return h("div", { className: cx("ks-progress", "ks-progress-" + s, ind && "ks-progress-indeterminate", p.className), role: "progressbar",
      "aria-label": p.label || "Progress", "aria-valuemin": 0, "aria-valuemax": 100, "aria-valuenow": ind ? undefined : Math.round(v) },
      h("div", { className: "ks-progress-fill", style: ind ? undefined : { width: v + "%" } }));
  }

  function formatBytes(b) {
    if (b == null || isNaN(b)) return "—";
    var u = ["B", "KB", "MB", "GB", "TB"], i = 0;
    while (b >= 1024 && i < u.length - 1) { b /= 1024; i++; }
    return (i === 0 || b >= 100 ? Math.round(b) : b.toFixed(1)) + " " + u[i];
  }
  function formatEta(s) {
    if (s == null || !isFinite(s)) return null;
    if (s < 60) return Math.ceil(s) + " s left";
    if (s < 3600) return Math.ceil(s / 60) + " min left";
    return Math.floor(s / 3600) + " h " + Math.round((s % 3600) / 60) + " min left";
  }

  function DownloadItem(p) {
    var s = p.status || "queued", size = p.size, done = p.downloaded || 0;
    var pct = size ? (done / size) * 100 : null;
    var meta;
    if (s === "completed") meta = formatBytes(size);
    else if (s === "failed") meta = p.error || "Download failed";
    else if (s === "queued") meta = "Waiting · " + formatBytes(size);
    else {
      var parts = [formatBytes(done) + " of " + formatBytes(size)];
      if (s === "downloading" && p.speed) parts.push(formatBytes(p.speed) + "/s");
      if (s === "downloading") { var e = formatEta(p.eta); if (e) parts.push(e); }
      meta = parts.join(" · ");
    }
    var A = [];
    function act(icon, label, fn) { A.push(h(IconButton, { key: label, icon: icon, label: label + " " + p.name, size: "sm", onClick: fn })); }
    if (s === "downloading") act("pause", "Pause", p.onPause);
    if (s === "paused") act("play", "Resume", p.onResume);
    if (s === "failed") act("retry", "Retry", p.onRetry);
    if (s === "completed") act("folder", "Show in folder", p.onOpen);
    if (s === "completed" || s === "failed") act("trash", "Remove", p.onRemove);
    else act("x", "Cancel", p.onCancel);
    return h("div", { className: cx("ks-item", p.className) },
      h("div", { className: "ks-tile" }, h(Icon, { name: p.fileType || "file" })),
      h("div", { className: "ks-item-main" },
        h("div", { className: "ks-item-head" }, h("span", { className: "ks-item-name", title: p.name }, p.name), h(StatusBadge, { status: s })),
        s !== "completed" && h(ProgressBar, { status: s, value: s === "failed" ? pct || 0 : pct, label: p.name + " progress" }),
        h("span", { className: cx("ks-item-meta", s === "failed" && "ks-item-meta-error") }, meta)),
      h("div", { className: "ks-item-actions" }, A));
  }

  function NavItem(p) {
    var rest = omit(p, ["icon", "label", "count", "active", "className"]);
    return h("button", Object.assign({ type: "button" }, rest, { className: cx("ks-nav", p.className), "aria-current": p.active ? "page" : undefined }),
      p.icon && h(Icon, { name: p.icon }), h("span", { className: "ks-nav-label" }, p.label),
      p.count != null && h("span", { className: "ks-nav-count" }, p.count));
  }

  function Toolbar(p) {
    return h("div", { className: cx("ks-toolbar", p.className), role: "toolbar", "aria-label": p.label || "Downloads" }, p.children);
  }
  Toolbar.Spacer = function () { return h("span", { className: "ks-toolbar-spacer" }); };

  function SpeedStat(p) {
    return h("div", { className: cx("ks-stat", p.className) },
      h("span", { className: "ks-stat-label" }, p.icon && h(Icon, { name: p.icon, size: 16 }), p.label),
      h("span", { className: "ks-stat-value" }, p.value, p.unit && h("span", { className: "ks-stat-unit" }, p.unit)));
  }

  function Dialog(p) {
    var ref = R.useRef(null), id = R.useId();
    R.useEffect(function () {
      if (!p.open) return;
      var prev = document.activeElement, el = ref.current;
      var first = el && el.querySelector("input,select,textarea,button:not(.ks-dialog-close)");
      (first || el).focus();
      function key(e) {
        if (e.key === "Escape" && p.onClose) p.onClose();
        if (e.key === "Tab" && el) { // keep focus inside the dialog
          var f = el.querySelectorAll("button,input,select,textarea,[href],[tabindex]:not([tabindex='-1'])");
          if (!f.length) return;
          var a = f[0], z = f[f.length - 1];
          if (e.shiftKey && document.activeElement === a) { e.preventDefault(); z.focus(); }
          else if (!e.shiftKey && document.activeElement === z) { e.preventDefault(); a.focus(); }
        }
      }
      document.addEventListener("keydown", key);
      return function () { document.removeEventListener("keydown", key); prev && prev.focus && prev.focus(); };
    }, [p.open]);
    if (!p.open) return null;
    return h("div", { className: "ks-scrim", onMouseDown: function (e) { if (e.target === e.currentTarget && p.onClose) p.onClose(); } },
      h("div", { ref: ref, className: "ks-dialog", role: "dialog", "aria-modal": true, "aria-labelledby": id, tabIndex: -1 },
        h("div", { className: "ks-dialog-head" }, h("h2", { className: "ks-dialog-title", id: id }, p.title),
          h(IconButton, { icon: "x", label: "Close", className: "ks-dialog-close", onClick: p.onClose })),
        h("div", { className: "ks-dialog-body" }, p.children),
        p.footer && h("div", { className: "ks-dialog-foot" }, p.footer)));
  }

  var TOAST_ICON = { info: "info", success: "check", error: "alert" };
  function Toast(p) {
    var t = p.tone || "info";
    return h("div", { className: cx("ks-toast", "ks-toast-" + t, p.className), role: t === "error" ? "alert" : "status" },
      h(Icon, { name: TOAST_ICON[t] }), h("span", { className: "ks-toast-msg" }, p.children),
      p.actionLabel && h("button", { type: "button", className: "ks-toast-action", onClick: p.onAction }, p.actionLabel),
      p.onClose && h(IconButton, { icon: "x", label: "Dismiss", size: "sm", onClick: p.onClose }));
  }

  function EmptyState(p) {
    return h("div", { className: cx("ks-empty", p.className) },
      h("div", { className: "ks-empty-art" }, h(Icon, { name: p.icon || "download", size: 28 })),
      h("h3", { className: "ks-empty-title" }, p.title),
      p.description && h("p", { className: "ks-empty-text" }, p.description),
      p.action);
  }

  window.Kestrel = Object.assign(window.Kestrel || {}, {
    Icon: Icon, Button: Button, IconButton: IconButton, TextField: TextField, Select: Select, Checkbox: Checkbox, Switch: Switch,
    StatusBadge: StatusBadge, ProgressBar: ProgressBar, DownloadItem: DownloadItem, NavItem: NavItem, Toolbar: Toolbar,
    SpeedStat: SpeedStat, Dialog: Dialog, Toast: Toast, EmptyState: EmptyState,
    formatBytes: formatBytes, iconNames: Object.keys(PATHS)
  });
})();
