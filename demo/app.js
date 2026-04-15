/* ── Config ── */
const API_BASE = 'http://localhost:5000';
const HUB_URL  = `${API_BASE}/hubs/notifications`;
const PAGE_SIZE = 20;

/* ── State ── */
const state = {
  userId:               null,
  connected:            false,
  notifications:        [],
  selectedId:           null,
  selectedNotification: null,
  unreadCount:          0,
  unreadOnly:           false,
  sending:              false,
  activeChannels:       new Set(['push']),
};

/* ── DOM refs ── */
const $ = id => document.getElementById(id);
const statusDot      = $('statusDot');
const userIdDisplay  = $('userIdDisplay');
const regenBtn       = $('regenBtn');
const unreadBadge    = $('unreadBadge');
const bellBtn        = $('bellBtn');
const inputTitle     = $('inputTitle');
const inputBody      = $('inputBody');
const addressFields  = $('addressFields');
const sendBtn        = $('sendBtn');
const notifList      = $('notifList');
const emptyState     = $('emptyState');
const toggleUnreadBtn= $('toggleUnreadBtn');
const markAllReadBtn = $('markAllReadBtn');
const detailPanel    = $('detailPanel');
const detailNotifName= $('detailNotifName');
const deliveriesGrid = $('deliveriesGrid');
const logEntries     = $('logEntries');

/* ── UUID ── */
function generateUUID() {
  return crypto.randomUUID
    ? crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
      });
}

function loadOrCreateUserId() {
  let id = localStorage.getItem('notifhub_userId');
  if (!id) { id = generateUUID(); localStorage.setItem('notifhub_userId', id); }
  return id;
}

function applyUserId(id) {
  state.userId = id;
  localStorage.setItem('notifhub_userId', id);
  userIdDisplay.textContent = id.substring(0, 18) + '…';
  userIdDisplay.title = id;
  // Push address field reflects current userId
  const pushInput = addressFields.querySelector('.addr-input.push');
  if (pushInput) pushInput.value = id;
}

/* ── SignalR ── */
let hub;

function buildHub() {
  return new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}

async function connectHub() {
  hub = buildHub();

  hub.on('NewNotification', notification => {
    log('ws', `<span class="green">NewNotification</span> → id: <span class="hi">${short(notification.id)}</span> · status: <span class="blue">${notification.status}</span>`);
    prependNotification(notification);
  });

  hub.on('UnreadCountUpdated', ({ count }) => {
    log('ws', `<span class="green">UnreadCountUpdated</span> → count: <span class="hi">${count}</span>`);
    setUnreadCount(count);
  });

  hub.onreconnecting(() => {
    state.connected = false;
    statusDot.classList.remove('connected');
    log('ws', '<span class="amber">reconnecting…</span>');
  });

  hub.onreconnected(async () => {
    state.connected = true;
    statusDot.classList.add('connected');
    log('ws', '<span class="green">reconnected</span>');
    await hub.invoke('JoinUserGroup', state.userId);
  });

  hub.onclose(() => {
    state.connected = false;
    statusDot.classList.remove('connected');
    log('ws', '<span class="red">disconnected</span>');
  });

  try {
    await hub.start();
    state.connected = true;
    statusDot.classList.add('connected');
    await hub.invoke('JoinUserGroup', state.userId);
    log('ws', '<span class="green">connected</span> · joined group <span class="hi">' + short(state.userId) + '</span>');
  } catch (err) {
    log('err', `connection failed: <span class="red">${err.message}</span>`);
  }
}

async function rejoinHub(newUserId) {
  if (hub && state.connected) {
    try { await hub.invoke('LeaveUserGroup', state.userId); } catch {}
  }
  applyUserId(newUserId);
  if (hub && state.connected) {
    await hub.invoke('JoinUserGroup', newUserId);
    log('ws', 'joined group <span class="hi">' + short(newUserId) + '</span>');
  }
}

/* ── API helpers ── */
async function apiFetch(method, path, body) {
  const opts = { method, headers: { 'Content-Type': 'application/json' } };
  if (body) opts.body = JSON.stringify(body);
  const url = `${API_BASE}${path}`;
  const res = await fetch(url, opts);
  return res;
}

/* ── Load inbox ── */
async function loadNotifications() {
  try {
    const res = await apiFetch('GET', `/notifications?userId=${state.userId}&page=1&pageSize=${PAGE_SIZE}`);
    log('api', `GET /notifications → <span class="${res.ok ? 'green' : 'red'}">${res.status}</span>`);
    if (!res.ok) return;
    const data = await res.json();
    state.notifications = data.items ?? [];
    renderInbox();
    // Unread count comes from WS but also sync on load
    const unread = state.notifications.filter(n => n.deliveries?.some(d => d.channel === 'push') && !n.isRead).length;
    // Use the badge from state which WS keeps in sync; only set if not yet set
    if (state.unreadCount === 0) setUnreadCount(unread);
  } catch (err) {
    log('err', `loadNotifications: <span class="red">${err.message}</span>`);
  }
}

/* ── Send notification ── */
async function sendNotification() {
  const title = inputTitle.value.trim();
  const body  = inputBody.value.trim();

  if (!title || !body) {
    inputTitle.focus();
    return;
  }

  if (state.activeChannels.size === 0) return;

  const channels = {};
  state.activeChannels.forEach(ch => {
    if (ch === 'push') {
      channels['push'] = state.userId;
    } else {
      const input = addressFields.querySelector(`.addr-input.${ch}`);
      channels[ch] = input ? input.value.trim() : '';
    }
  });

  // Validate addresses
  for (const [ch, addr] of Object.entries(channels)) {
    if (!addr) {
      log('err', `<span class="red">missing address for channel: ${ch}</span>`);
      return;
    }
  }

  state.sending = true;
  sendBtn.disabled = true;
  sendBtn.textContent = '⏳ Sending…';

  try {
    const res = await apiFetch('POST', '/notifications', {
      recipientUserId: state.userId,
      title,
      body,
      channels,
    });

    const data = res.ok ? await res.json() : null;
    const statusClass = res.ok ? 'green' : 'red';
    const idPart = data ? ` · id: <span class="hi">${short(data.id)}</span>` : '';
    log('api', `POST /notifications → <span class="${statusClass}">${res.status}</span>${idPart}`);

    if (res.ok) {
      sendBtn.textContent = '✓ Sent';
      sendBtn.classList.add('flash');
      setTimeout(() => {
        sendBtn.classList.remove('flash');
        sendBtn.textContent = '⚡ Send Notification';
      }, 1500);
    } else {
      sendBtn.textContent = '⚡ Send Notification';
    }
  } catch (err) {
    log('err', `send failed: <span class="red">${err.message}</span>`);
    sendBtn.textContent = '⚡ Send Notification';
  } finally {
    state.sending = false;
    sendBtn.disabled = false;
  }
}

/* ── Mark as read ── */
async function markAsRead(id) {
  try {
    const res = await apiFetch('PATCH', `/notifications/${id}/read`);
    log('api', `PATCH /notifications/${short(id)}/read → <span class="${res.ok ? 'green' : 'red'}">${res.status}</span>`);
    if (res.ok) {
      const n = state.notifications.find(x => x.id === id);
      if (n) { n.isRead = true; n.readAt = new Date().toISOString(); }
      renderInbox();
      if (state.selectedId === id) renderDetailPanel();
    }
  } catch (err) {
    log('err', `markAsRead: <span class="red">${err.message}</span>`);
  }
}

async function markAllAsRead() {
  try {
    const res = await apiFetch('PATCH', `/notifications/read-all?userId=${state.userId}`);
    log('api', `PATCH /notifications/read-all → <span class="${res.ok ? 'green' : 'red'}">${res.status}</span>`);
    if (res.ok) {
      state.notifications.forEach(n => { n.isRead = true; });
      setUnreadCount(0);
      renderInbox();
    }
  } catch (err) {
    log('err', `markAllAsRead: <span class="red">${err.message}</span>`);
  }
}

/* ── Select notification (load detail) ── */
async function selectNotification(id) {
  state.selectedId = id;
  renderInbox();

  try {
    const res = await apiFetch('GET', `/notifications/${id}`);
    if (!res.ok) return;
    state.selectedNotification = await res.json();
    renderDetailPanel();
  } catch (err) {
    log('err', `getNotification: <span class="red">${err.message}</span>`);
  }
}

/* ── Render: Inbox ── */
function renderInbox() {
  const items = state.unreadOnly
    ? state.notifications.filter(n => !n.isRead)
    : state.notifications;

  emptyState.style.display = items.length === 0 ? 'block' : 'none';

  // Diff render: remove items no longer visible, add new ones
  const existingIds = new Set([...notifList.querySelectorAll('.notif-item')].map(el => el.dataset.id));
  const newIds      = new Set(items.map(n => n.id));

  // Remove stale
  notifList.querySelectorAll('.notif-item').forEach(el => {
    if (!newIds.has(el.dataset.id)) el.remove();
  });

  // Update / insert
  items.forEach((n, idx) => {
    let el = notifList.querySelector(`.notif-item[data-id="${n.id}"]`);
    const classes = ['notif-item'];
    if (!n.isRead) classes.push('unread');
    if (n.isRead)  classes.push('read');
    if (n.id === state.selectedId) classes.push('selected');

    if (!el) {
      el = document.createElement('div');
      el.dataset.id = n.id;
      el.addEventListener('click', () => handleNotifClick(n.id));
    }

    el.className = classes.join(' ');
    el.innerHTML = notifItemHTML(n);

    // Insert at correct position
    const existing = notifList.children[idx + 1]; // +1 for emptyState
    const refEl = notifList.querySelectorAll('.notif-item')[idx];
    if (!refEl || refEl.dataset.id !== n.id) {
      notifList.insertBefore(el, notifList.querySelectorAll('.notif-item')[idx] || null);
    }
  });
}

function notifItemHTML(n) {
  const time = relativeTime(n.createdAt);
  const channels = [...new Set((n.deliveries ?? []).map(d => d.channel))];
  const statusTag = `<span class="tag s-${n.status}">${n.status}</span>`;
  const channelTags = channels.map(ch => `<span class="tag ch ${ch}">${ch}</span>`).join('');
  return `
    <div class="notif-top">
      <span class="notif-title-text">${esc(n.title)}</span>
      <span class="notif-time">${time}</span>
    </div>
    <div class="notif-body">${esc(n.body)}</div>
    <div class="notif-meta">${statusTag}${channelTags}</div>
  `;
}

async function handleNotifClick(id) {
  const n = state.notifications.find(x => x.id === id);
  if (n && !n.isRead) await markAsRead(id);
  await selectNotification(id);
}

/* ── Render: Detail Panel ── */
function renderDetailPanel() {
  const n = state.selectedNotification;
  if (!n) { detailPanel.classList.add('hidden'); return; }

  detailPanel.classList.remove('hidden');
  detailNotifName.textContent = n.title;

  deliveriesGrid.innerHTML = (n.deliveries ?? []).map(d => {
    const dotClass = d.status === 'sent' ? 'sent' : d.status === 'pending' ? 'pending' : 'failed';
    const errHtml  = d.errorMessage ? `<div class="delivery-error">${esc(d.errorMessage)}</div>` : '';
    const retryHtml= d.retryCount > 0 ? `<div class="delivery-retry">retries: ${d.retryCount}</div>` : '';
    return `
      <div class="delivery-card">
        <div class="delivery-card-top">
          <div class="delivery-dot ${dotClass}"></div>
          <span class="delivery-channel">${d.channel}</span>
          <span class="delivery-status ${dotClass}">${d.status}</span>
        </div>
        <div class="delivery-recipient" title="${esc(d.recipient)}">${esc(d.recipient)}</div>
        ${errHtml}${retryHtml}
      </div>`;
  }).join('');
}

/* ── Render: Bell badge ── */
function setUnreadCount(count) {
  state.unreadCount = count;
  unreadBadge.textContent = count;
  unreadBadge.classList.toggle('zero', count === 0);
}

/* ── Prepend incoming notification ── */
function prependNotification(notification) {
  // Remove if already exists (e.g. duplicate events)
  state.notifications = state.notifications.filter(n => n.id !== notification.id);
  state.notifications.unshift(notification);
  renderInbox();
}

/* ── Activity Log ── */
function log(tag, msgHtml) {
  const ts = new Date().toLocaleTimeString('en-US', { hour12: false });
  const line = document.createElement('div');
  line.className = 'log-line';
  line.innerHTML = `<span class="log-ts">${ts}</span><span class="log-tag ${tag}">${tag.toUpperCase()}</span><span class="log-msg">${msgHtml}</span>`;
  logEntries.prepend(line);
  // Trim to 100 entries
  while (logEntries.children.length > 100) logEntries.lastChild.remove();
}

/* ── Channel toggles & address fields ── */
function renderAddressFields() {
  addressFields.innerHTML = '';
  if (state.activeChannels.size === 0) return;

  state.activeChannels.forEach(ch => {
    const wrap = document.createElement('div');
    wrap.className = 'addr-group';

    const label = document.createElement('span');
    label.className = `addr-label ${ch}`;
    label.textContent = ch === 'push' ? `push recipient (userId)` : ch;
    wrap.appendChild(label);

    const input = document.createElement('input');
    input.className = `addr-input field-input ${ch}`;
    input.type = 'text';

    if (ch === 'push') {
      input.value = state.userId;
      input.readOnly = true;
    } else {
      const saved = localStorage.getItem(`notifhub_addr_${ch}`) || '';
      input.value = saved;
      input.placeholder = ch === 'email' ? 'user@example.com'
                        : ch === 'sms'   ? '+1 555 000 0000'
                        : '+1 555 000 0000';
      input.addEventListener('input', () => {
        localStorage.setItem(`notifhub_addr_${ch}`, input.value);
      });
    }

    wrap.appendChild(input);
    addressFields.appendChild(wrap);
  });
}

document.querySelectorAll('.channel-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    const ch = btn.dataset.channel;
    if (state.activeChannels.has(ch)) {
      if (state.activeChannels.size === 1) return; // at least one must stay active
      state.activeChannels.delete(ch);
      btn.classList.remove('active');
    } else {
      state.activeChannels.add(ch);
      btn.classList.add('active');
    }
    renderAddressFields();
  });
});

/* ── Misc UI ── */
regenBtn.addEventListener('click', async () => {
  regenBtn.classList.add('spinning');
  setTimeout(() => regenBtn.classList.remove('spinning'), 500);
  const newId = generateUUID();
  await rejoinHub(newId);
  state.notifications = [];
  state.selectedId = null;
  state.selectedNotification = null;
  setUnreadCount(0);
  renderInbox();
  detailPanel.classList.add('hidden');
  renderAddressFields();
  await loadNotifications();
});

bellBtn.addEventListener('click', () => {
  document.getElementById('inbox').scrollIntoView({ behavior: 'smooth' });
});

toggleUnreadBtn.addEventListener('click', () => {
  state.unreadOnly = !state.unreadOnly;
  toggleUnreadBtn.classList.toggle('active', state.unreadOnly);
  renderInbox();
});

markAllReadBtn.addEventListener('click', markAllAsRead);
sendBtn.addEventListener('click', sendNotification);

inputTitle.addEventListener('keydown', e => { if (e.key === 'Enter') inputBody.focus(); });
inputBody.addEventListener('keydown', e => { if (e.key === 'Enter' && e.ctrlKey) sendNotification(); });

/* ── Utils ── */
function short(id) {
  return id ? id.substring(0, 8) + '…' : '';
}

function esc(str) {
  if (!str) return '';
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function relativeTime(iso) {
  if (!iso) return '';
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1)  return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24)  return `${hrs}h ago`;
  return new Date(iso).toLocaleDateString();
}

/* ── Init ── */
async function init() {
  const id = loadOrCreateUserId();
  applyUserId(id);
  renderAddressFields();
  await connectHub();
  await loadNotifications();
}

init();
