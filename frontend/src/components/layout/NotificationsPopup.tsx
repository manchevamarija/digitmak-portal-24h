import { useEffect, useRef, useState } from "react";
import type { Language } from "../../shared/types";
import { localeFor } from "../../content/dashboardCopy";

export type NotificationItem = {
  id: string; type: string; subject: string; body: string;
  actionUrl?: string | null; isRead: boolean; createdAt: string;
};

const labels = {
  mk: { title: "Известувања", markAll: "Означи ги сите како прочитани", all: "Сите", unread: "Непрочитани", emptyTitle: "Сè е прегледано", emptyBody: "Тука ќе добиете известување за тикет, организација или закажан состанок.", emptyUnread: "Нема непрочитани известувања.", open: "Отвори", delete: "Избриши известување" },
  en: { title: "Notifications", markAll: "Mark all as read", all: "All", unread: "Unread", emptyTitle: "You're all caught up", emptyBody: "Ticket, organisation and scheduled meeting updates will appear here.", emptyUnread: "No unread notifications.", open: "Open", delete: "Delete notification" },
  sq: { title: "Njoftimet", markAll: "Shëno të gjitha si të lexuara", all: "Të gjitha", unread: "Të palexuara", emptyTitle: "Gjithçka është kontrolluar", emptyBody: "Këtu shfaqen njoftimet për tiketën, organizatën ose takimin.", emptyUnread: "Nuk ka njoftime të palexuara.", open: "Hap", delete: "Fshi njoftimin" },
} satisfies Record<Language, Record<string, string>>;

const plainText = (html: string) =>
  new DOMParser().parseFromString(html, "text/html").body.textContent?.trim() ?? "";

function destination(item: NotificationItem, language: Language) {
  const value = `${item.type} ${item.actionUrl ?? ""}`.toLowerCase();
  if (value.includes("ticket")) return language === "mk" ? "Тикет" : language === "sq" ? "Tiketë" : "Ticket";
  if (value.includes("meeting")) return language === "mk" ? "Состанок" : language === "sq" ? "Takim" : "Meeting";
  if (value.includes("org") || value.includes("subscription")) return language === "mk" ? "Организација" : language === "sq" ? "Organizatë" : "Organisation";
  return labels[language].open;
}

export function NotificationsPopup({ items, language, onClose, onOpenItem, onMarkAllRead, onDelete }: {
  items: NotificationItem[]; language: Language; onClose: () => void;
  onOpenItem: (item: NotificationItem) => void; onMarkAllRead: () => void;
  onDelete: (item: NotificationItem) => void;
}) {
  const t = labels[language];
  const [filter, setFilter] = useState<"all" | "unread">("all");
  const rootRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const outside = (event: MouseEvent) => { if (!rootRef.current?.contains(event.target as Node)) onClose(); };
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape") onClose(); };
    document.addEventListener("mousedown", outside); document.addEventListener("keydown", escape);
    return () => { document.removeEventListener("mousedown", outside); document.removeEventListener("keydown", escape); };
  }, [onClose]);
  const visible = filter === "unread" ? items.filter((item) => !item.isRead) : items;
  const unreadCount = items.filter((item) => !item.isRead).length;
  return (
    <div className="notifications-popup" ref={rootRef} role="dialog" aria-label={t.title}>
      <div className="notifications-popup-head">
        <strong>{t.title}</strong>
        {unreadCount > 0 && <button className="notifications-popup-mark-all" onClick={onMarkAllRead}>{t.markAll}</button>}
      </div>
      <div className="notifications-popup-tabs">
        <button className={filter === "all" ? "active" : ""} onClick={() => setFilter("all")}>{t.all} <span>{items.length}</span></button>
        <button className={filter === "unread" ? "active" : ""} onClick={() => setFilter("unread")}>{t.unread} <span>{unreadCount}</span></button>
      </div>
      <div className="notifications-popup-body">
        {visible.length === 0 ? (
          <div className="notifications-popup-empty">
            <span className="notifications-empty-icon" aria-hidden="true">✓</span>
            <strong>{filter === "unread" ? t.emptyUnread : t.emptyTitle}</strong>
            {filter === "all" && <span>{t.emptyBody}</span>}
          </div>
        ) : <ul>{visible.map((item) => (
          <li key={item.id} className="notifications-popup-item">
            <button className={item.isRead ? "read" : "unread"} onClick={() => onOpenItem(item)}>
              <span className="notifications-popup-row"><span className="notifications-popup-subject">{item.subject}</span>{!item.isRead && <span className="notification-dot" aria-label={t.unread} />}</span>
              <span className="notifications-popup-message">{plainText(item.body)}</span>
              <span className="notifications-popup-meta"><span>{new Date(item.createdAt).toLocaleString(localeFor(language))}</span>{item.actionUrl && <b>{destination(item, language)} →</b>}</span>
            </button>
            <button
              type="button"
              className="notifications-popup-delete"
              aria-label={t.delete}
              title={t.delete}
              onClick={(event) => { event.stopPropagation(); onDelete(item); }}
            >
              ×
            </button>
          </li>
        ))}</ul>}
      </div>
    </div>
  );
}
