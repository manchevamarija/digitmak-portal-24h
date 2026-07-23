import { useState } from "react";
import type { FormEvent } from "react";
import { api } from "../../api";
import { MeetingWorkspace } from "../../features/meetings/MeetingWorkspace";
import { TicketWorkspace } from "../../features/tickets/TicketWorkspace";
import { useAuth } from "../../features/auth/useAuth";
import type {
  Meeting,
  Organization,
  Profile,
  Subscription,
  SubscriptionInvitation,
  Ticket,
} from "../../shared/domain";
import type { Navigate } from "../../shared/types";
import { useApiResource } from "../../shared/useApiResource";
import { labelFor, ticketStatusClass } from "../../shared/labels";
import { usePortalLanguage } from "../../shared/usePortalLanguage";
import { dashboardCopy, localeFor } from "../../content/dashboardCopy";
import { NotificationsPopup } from "../../components/layout/NotificationsPopup";
export type Tab =
  | "overview"
  | "organization"
  | "tickets"
  | "meetings"
  | "notifications"
  | "profile";
type PaymentInstructions = {
  isConfigured: boolean;
  recipient: string;
  bank: string;
  account: string;
  iban: string;
  swift: string;
  amount: string;
  currency: string;
  purpose: string;
  referenceInstruction: string;
  supportEmail: string;
};
type AccountChangeRequest = {
  id: string;
  requestType: "Organization" | "Subscription";
  details: string;
  status: "Pending" | "Accepted" | "Declined" | "Applied";
  decisionNote?: string;
  createdAt: string;
  decidedAt?: string;
};
type NotificationItem = {
  id: string;
  type: string;
  subject: string;
  body: string;
  actionUrl?: string | null;
  isRead: boolean;
  createdAt: string;
};
export function ClientDashboardPage({
  onNavigate,
  initialTab,
  initialTicketId: initialTicketIdProp,
}: {
  onNavigate: Navigate;
  initialTab?: Tab;
  initialTicketId?: string;
}) {
  const language = usePortalLanguage();
  const t = dashboardCopy[language].client;
  const { user, logout, isAuthenticated } = useAuth();
  const managesClientTickets = Boolean(
    user?.roles.some((role) =>
      ["Admin", "HelpDeskAgent", "Expert"].includes(role),
    ),
  );
  const managedTicketLabel =
    language === "mk" ? "Тикети" : language === "sq" ? "Tiketat" : "Tickets";
  const [tab, setTab] = useState<Tab>(
    initialTicketIdProp ? "tickets" : (initialTab ?? "overview"),
  );
  const [initialTicketId, setInitialTicketId] = useState<string | undefined>(
    initialTicketIdProp,
  );
  const [version, setVersion] = useState(0);
  const refresh = () => setVersion((value) => value + 1);
  const tickets = useApiResource<Ticket[]>(
    managesClientTickets
      ? `/api/staff/tickets?pageSize=100&v=${version}`
      : `/api/tickets/my?v=${version}`,
    isAuthenticated,
  );
  const isMeetingAdmin = Boolean(user?.roles.includes("Admin"));
  const meetings = useApiResource<Meeting[]>(
    isMeetingAdmin
      ? `/api/admin/meetings/mine?v=${version}`
      : `/api/meetings/my?v=${version}`,
    isAuthenticated,
  );
  const organization = useApiResource<Organization>(
    `/api/organizations/my?v=${version}`,
    isAuthenticated,
  );
  const subscription = useApiResource<Subscription>(
    `/api/subscriptions/my?v=${version}`,
    isAuthenticated,
  );
  const paymentInstructions = useApiResource<PaymentInstructions>(
    `/api/subscriptions/payment-instructions?v=${version}`,
    isAuthenticated && subscription.data?.status === "PendingPayment",
  );
  const invitation = useApiResource<SubscriptionInvitation>(
    `/api/subscriptions/invitations/my?v=${version}`,
    isAuthenticated,
  );
  const profile = useApiResource<Profile>(
    `/api/profile/?v=${version}`,
    isAuthenticated,
  );
  const accountChanges = useApiResource<AccountChangeRequest[]>(
    `/api/account-change-requests/my?v=${version}`,
    isAuthenticated,
  );
  const notifications = useApiResource<NotificationItem[]>(
    `/api/notifications/mine?v=${version}`,
    isAuthenticated,
  );
  const unreadNotifications = (notifications.data ?? []).filter(
    (item) => !item.isRead,
  ).length;
  const [notificationsPopupOpen, setNotificationsPopupOpen] = useState(false);
  const openNotification = async (item: NotificationItem) => {
    if (!item.isRead) {
      try {
        await api(`/api/notifications/${item.id}/read`, { method: "POST" });
        refresh();
      } catch {
        // Non-fatal — navigation still proceeds even if marking-read failed.
      }
    }
    if (item.actionUrl) window.location.href = item.actionUrl;
    else setNotificationsPopupOpen(false);
  };
  const markAllNotificationsRead = async () => {
    try {
      await api("/api/notifications/read-all", { method: "POST" });
      refresh();
    } catch {
      // Non-fatal — the popup simply keeps showing the previous read state.
    }
  };
  const deleteNotification = async (item: NotificationItem) => {
    try {
      await api(`/api/notifications/${item.id}`, { method: "DELETE" });
      refresh();
    } catch {
      // Non-fatal — the item simply stays in the list if the delete failed.
    }
  };
  const items = tickets.data ?? [];
  const canCreateTickets =
    organization.data?.status === "Approved" &&
    subscription.data?.status === "Active" &&
    Boolean(subscription.data.expiresAt) &&
    new Date(subscription.data.expiresAt!).getTime() > Date.now();
  const menu: { key: Tab; label: string }[] = [
    { key: "overview", label: t.overview },
    {
      key: "tickets",
      label: managesClientTickets ? managedTicketLabel : t.tickets,
    },
    { key: "organization", label: t.organizationSubscription },
    { key: "meetings", label: t.meetings },
    { key: "notifications", label: t.notifications },
    { key: "profile", label: t.profile },
  ];
  const openTickets = (ticketId?: string) => {
    if (managesClientTickets) {
      onNavigate(user?.roles.includes("Admin") ? "admin" : "staff", {
        tab: "tickets",
        ticket: ticketId,
      });
      return;
    }
    setInitialTicketId(ticketId);
    setTab("tickets");
  };
  return (
    <section className="dashboard">
      <aside>
        <div className="user">
          <span>{user ? `${user.firstName[0]}${user.lastName[0]}` : "КП"}</span>
          <div>
            <b>{user ? `${user.firstName} ${user.lastName}` : t.user}</b>
            <small>{t.signedIn}</small>
          </div>
        </div>
        {menu.map((item) =>
          item.key === "notifications" ? (
            <div className="notifications-anchor" key={item.key}>
              <button
                className={notificationsPopupOpen ? "sel" : ""}
                onClick={() => setNotificationsPopupOpen((value) => !value)}
              >
                <span>
                  {item.label}
                  {unreadNotifications > 0 && (
                    <span className="menu-badge">{unreadNotifications}</span>
                  )}
                </span>
                <span>›</span>
              </button>
              {notificationsPopupOpen && (
                <NotificationsPopup
                  items={notifications.data ?? []}
                  language={language}
                  onClose={() => setNotificationsPopupOpen(false)}
                  onOpenItem={openNotification}
                  onMarkAllRead={markAllNotificationsRead}
                  onDelete={deleteNotification}
                />
              )}
            </div>
          ) : (
            <button
              className={tab === item.key ? "sel" : ""}
              key={item.key}
              onClick={() =>
                item.key === "tickets" ? openTickets() : setTab(item.key)
              }
            >
              <span>{item.label}</span>
              <span>›</span>
            </button>
          ),
        )}
        {!organization.data && (
          <button onClick={() => onNavigate("organization")}>
            {t.registerOrganization} <span>›</span>
          </button>
        )}
        {user?.roles.includes("Admin") && (
          <button onClick={() => onNavigate("admin")}>
            {t.administration} <span>›</span>
          </button>
        )}
        {user?.roles.some((role) =>
          ["HelpDeskAgent", "Expert"].includes(role),
        ) && (
          <button onClick={() => onNavigate("staff")}>
            {t.workspace} <span>›</span>
          </button>
        )}
        <button
          className="logout"
          onClick={async () => {
            await logout();
            onNavigate("home");
          }}
        >
          {t.logout}
        </button>
      </aside>
      <div className="dash-main">
        <div className="dash-head">
          <div>
            <span>{t.portal}</span>
            <h1>
              {tab === "overview"
                ? `${t.welcome}, ${user?.firstName ?? t.user}.`
                : menu.find((x) => x.key === tab)?.label}
            </h1>
          </div>
          {tab !== "tickets" && (
            <button className="primary" onClick={() => openTickets()}>
              + {t.newTicket}
            </button>
          )}
        </div>
        {tab === "overview" && (
          <>
            <div className="stats">
              <article>
                <span>{t.activeTickets}</span>
                <b>
                  {
                    items.filter(
                      (x) => !["Closed", "Resolved"].includes(x.status),
                    ).length
                  }
                </b>
                <small>
                  {tickets.loading
                    ? t.loading
                    : tickets.error || t.activeRequests}
                </small>
              </article>
              <article>
                <span>{t.nextMeeting}</span>
                <b className="date">
                  {meetings.data?.find((x) => x.startsAt)?.startsAt
                    ? new Date(
                        meetings.data.find((x) => x.startsAt)!.startsAt!,
                      ).toLocaleDateString(localeFor(language))
                    : "—"}
                </b>
                <small>
                  {meetings.data?.find((x) => x.startsAt)?.subject ??
                    t.noConfirmed}
                </small>
              </article>
              <article>
                <span>{t.subscription}</span>
                <b className="ok">
                  {subscription.data?.status
                    ? labelFor(subscription.data.status, language)
                    : invitation.data
                      ? t.invitationUpper
                      : t.inactiveUpper}
                </b>
                <small>
                  {subscription.data?.expiresAt
                    ? `${t.validUntil} ${new Date(subscription.data.expiresAt).toLocaleDateString(localeFor(language))}`
                    : t.annual}
                </small>
              </article>
            </div>
            <div className="ticket-list">
              <div className="list-head">
                <h2>{t.recentTickets}</h2>
                <button onClick={() => openTickets()}>{t.seeAll}</button>
              </div>
              {items.slice(0, 5).map((ticket) => (
                <button
                  className="ticket ticket-button"
                  key={ticket.id}
                  onClick={() => openTickets(ticket.id)}
                >
                  <span className={`tag ${ticketStatusClass(ticket.status)}`}>
                    {labelFor(ticket.status, language)}
                  </span>
                  <div>
                    <b>{ticket.title}</b>
                    <small>
                      #{ticket.ticketNumber} ·{" "}
                      {labelFor(ticket.category, language)}
                    </small>
                  </div>
                  <span>
                    {new Date(ticket.updatedAt).toLocaleDateString(
                      localeFor(language),
                    )}
                  </span>
                </button>
              ))}
              {!items.length && (
                <div className="empty-state">{t.noTickets}</div>
              )}
            </div>
          </>
        )}
        {tab === "organization" && (
          <OrganizationPanel
            organization={organization.data}
            subscription={subscription.data}
            invitation={invitation.data}
            paymentInstructions={paymentInstructions.data}
            changeRequests={accountChanges.data ?? []}
            onNavigate={onNavigate}
            onChanged={refresh}
          />
        )}
        {tab === "tickets" && (
          <TicketWorkspace
            tickets={items}
            onChanged={refresh}
            canCreate={canCreateTickets}
            accessMessage={dashboardCopy[language].ticket.accessRequired}
            initialTicketId={initialTicketId}
          />
        )}
        {tab === "meetings" && (
          <MeetingWorkspace
            meetings={meetings.data ?? []}
            onChanged={refresh}
            adminMode={user?.roles.includes("Admin")}
          />
        )}
        {tab === "profile" && (
          <ProfilePanel profile={profile.data} onChanged={refresh} />
        )}
      </div>
    </section>
  );
}


function OrganizationPanel({
  organization,
  subscription,
  invitation,
  paymentInstructions,
  changeRequests,
  onNavigate,
  onChanged,
}: {
  organization: Organization | null;
  subscription: Subscription | null;
  invitation: SubscriptionInvitation | null;
  paymentInstructions: PaymentInstructions | null;
  changeRequests: AccountChangeRequest[];
  onNavigate: Navigate;
  onChanged: () => void;
}) {
  const language = usePortalLanguage();
  const t = dashboardCopy[language].client;
  const [subscriptionError, setSubscriptionError] = useState("");
  const [changeError, setChangeError] = useState("");
  const [showChangeRequest, setShowChangeRequest] = useState(false);
  const [changeRequestSent, setChangeRequestSent] = useState(false);
  const availableOrganizations = useApiResource<
    Pick<Organization, "id" | "name" | "type" | "region">[]
  >("/api/organizations/available");
  if (!organization)
    return (
      <div className="empty-panel">
        <h2>{t.noOrganization}</h2>
        <p>{t.createOrJoin}</p>
        <button className="primary" onClick={() => onNavigate("organization")}>
          {t.organization}
        </button>
      </div>
    );
  const accept = async () => {
    if (!invitation) return;
    try {
      await api(`/api/subscriptions/${invitation.id}/accept`, {
        method: "POST",
      });
      onChanged();
    } catch (reason) {
      setSubscriptionError(
        reason instanceof Error ? reason.message : t.invitationError,
      );
    }
  };
  const requestChange = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const requestType = String(data.get("requestType"));
    const details = String(data.get("details") ?? "").trim();
    try {
      await api("/api/account-change-requests/", {
        method: "POST",
        body: JSON.stringify({
          requestType:
            requestType === "organization" ? "Organization" : "Subscription",
          details,
        }),
      });
      form.reset();
      setShowChangeRequest(false);
      setChangeRequestSent(true);
      setChangeError("");
      onChanged();
    } catch (reason) {
      setChangeError(
        reason instanceof Error
          ? reason.message
          : language === "mk"
            ? "Барањето не може да се испрати."
            : "The request could not be sent.",
      );
    }
  };
  const canAcceptInvitation =
    invitation != null &&
    (!subscription || ["Cancelled", "Expired"].includes(subscription.status));
  return (
    <div className="workspace two-column">
      <article className="detail-card">
        <span className="kicker">{t.organization.toUpperCase()}</span>
        <h2>{organization.name}</h2>
        <dl>
          <div>
            <dt>{t.type}</dt>
            <dd>{labelFor(organization.type, language)}</dd>
          </div>
          <div>
            <dt>{t.sector}</dt>
            <dd>{organization.sector || "—"}</dd>
          </div>
          <div>
            <dt>{t.region}</dt>
            <dd>{organization.region || "—"}</dd>
          </div>
          <div>
            <dt>{t.status}</dt>
            <dd>{labelFor(organization.status, language)}</dd>
          </div>
        </dl>
      </article>
      <article className="detail-card">
        <span className="kicker">{t.subscription.toUpperCase()}</span>
        <h2>
          {canAcceptInvitation
            ? t.invitation
            : subscription?.status
              ? labelFor(subscription.status, language)
              : t.noInvitation}
        </h2>
        {canAcceptInvitation && invitation && (
          <>
            <p>
              {t.invitationUntil}{" "}
              {new Date(invitation.expiresAt).toLocaleDateString(
                localeFor(language),
              )}
              .
            </p>
            <button className="primary" onClick={accept}>
              {t.acceptInvitation}
            </button>
          </>
        )}
        {subscription?.status === "PendingPayment" && (
          <>
            <p>{t.pendingPayment}</p>
            {paymentInstructions?.isConfigured ? (
              <section className="payment-instructions">
                <h3>{t.paymentInstructions}</h3>
                <dl>
                  <PaymentRow
                    label={t.paymentRecipient}
                    value={paymentInstructions.recipient}
                  />
                  <PaymentRow
                    label={t.paymentBank}
                    value={paymentInstructions.bank}
                  />
                  <PaymentRow
                    label={t.paymentAccount}
                    value={paymentInstructions.account}
                  />
                  <PaymentRow label="IBAN" value={paymentInstructions.iban} />
                  <PaymentRow label="SWIFT" value={paymentInstructions.swift} />
                  <PaymentRow
                    label={t.paymentAmount}
                    value={`${paymentInstructions.amount} ${paymentInstructions.currency}`.trim()}
                  />
                  <PaymentRow
                    label={t.paymentPurpose}
                    value={paymentInstructions.purpose}
                  />
                  <PaymentRow
                    label={t.paymentReference}
                    value={paymentInstructions.referenceInstruction}
                  />
                </dl>
                {paymentInstructions.supportEmail && (
                  <p>
                    {t.paymentHelp}: {paymentInstructions.supportEmail}
                  </p>
                )}
              </section>
            ) : (
              <p className="notice padded">{t.paymentNotConfigured}</p>
            )}
          </>
        )}
        {subscription?.status === "Active" && (
          <p>
            {t.activeUntil}{" "}
            {subscription.expiresAt
              ? new Date(subscription.expiresAt).toLocaleDateString(
                  localeFor(language),
                )
              : "—"}
            .
          </p>
        )}
        {!subscription && !invitation && <p>{t.invitationMissing}</p>}
        {subscriptionError && <p className="form-error">{subscriptionError}</p>}
      </article>
      <article className="detail-card change-request-card">
        <div>
          <span className="kicker">
            {language === "mk"
              ? "ПРОМЕНА НА ПОДАТОЦИ"
              : language === "sq"
                ? "NDRYSHIM I TË DHËNAVE"
                : "ACCOUNT CHANGES"}
          </span>
          <h2>
            {language === "mk"
              ? "Организација или претплата"
              : language === "sq"
                ? "Organizata ose abonimi"
                : "Organization or subscription"}
          </h2>
          <p>
            {language === "mk"
              ? "Испратете барање до администраторот ако сакате да ја смените организацијата или условите на претплатата. Тековниот пристап останува активен додека барањето се разгледува."
              : language === "sq"
                ? "Dërgoni një kërkesë te administratori për të ndryshuar organizatën ose abonimin. Qasja aktuale mbetet aktive gjatë shqyrtimit."
                : "Send a request to the administrator to change the organization or subscription. Current access remains active while it is reviewed."}
          </p>
        </div>
        {!showChangeRequest && (
          <button
            type="button"
            className="secondary"
            onClick={() => {
              setShowChangeRequest(true);
              setChangeRequestSent(false);
            }}
          >
            {language === "mk"
              ? "Побарај промена"
              : language === "sq"
                ? "Kërko ndryshim"
                : "Request a change"}
          </button>
        )}
        {showChangeRequest && (
          <form className="change-request-form" onSubmit={requestChange}>
            <label>
              {language === "mk" ? "Што сакате да смените?" : "Change type"}
              <select name="requestType" required>
                <option value="organization">
                  {language === "mk" ? "Организација" : "Organization"}
                </option>
                <option value="subscription">
                  {language === "mk" ? "Претплата" : "Subscription"}
                </option>
              </select>
            </label>
            <label>
              {language === "mk" ? "Опис на промената" : "Change details"}
              <textarea name="details" rows={4} required minLength={10} />
            </label>
            <div className="action-row">
              <button className="primary">
                {language === "mk" ? "Испрати барање" : "Send request"}
              </button>
              <button
                type="button"
                className="secondary"
                onClick={() => setShowChangeRequest(false)}
              >
                {language === "mk" ? "Откажи" : "Cancel"}
              </button>
            </div>
          </form>
        )}
        {changeRequestSent && (
          <p className="form-success">
            {language === "mk"
              ? "Барањето е испратено. Статусот се прикажува подолу на оваа страница."
              : "The request was sent. Its status is shown below on this page."}
          </p>
        )}
        {!!changeRequests.length && (
          <div className="account-change-list">
            <h3>
              {language === "mk"
                ? "Мои барања за промена"
                : language === "sq"
                  ? "Kërkesat e mia për ndryshim"
                  : "My change requests"}
            </h3>
            {changeRequests.map((item) => (
              <article className="account-change-item" key={item.id}>
                <div>
                  <b>
                    {labelFor(item.requestType, language)} ·{" "}
                    {item.status === "Pending"
                      ? language === "mk"
                        ? "Се разгледува"
                        : language === "sq"
                          ? "Në shqyrtim"
                          : "Under review"
                      : item.status === "Accepted"
                        ? language === "mk"
                          ? "Одобрено"
                          : language === "sq"
                            ? "Miratuar"
                            : "Approved"
                        : item.status === "Applied"
                          ? language === "mk"
                            ? "Применето"
                            : language === "sq"
                              ? "U zbatua"
                              : "Applied"
                          : language === "mk"
                            ? "Одбиено"
                            : language === "sq"
                              ? "Refuzuar"
                              : "Declined"}
                  </b>
                  <p>{item.details}</p>
                  {item.decisionNote && <small>{item.decisionNote}</small>}
                </div>
                {item.status === "Accepted" &&
                  item.requestType === "Organization" && (
                    <form
                      className="account-change-apply"
                      onSubmit={async (event) => {
                        event.preventDefault();
                        const form = event.currentTarget;
                        const organizationId = String(
                          new FormData(form).get("organizationId") ?? "",
                        );
                        try {
                          await api(
                            `/api/account-change-requests/${item.id}/apply-organization`,
                            {
                              method: "POST",
                              body: JSON.stringify({ organizationId }),
                            },
                          );
                          setChangeError("");
                          onChanged();
                        } catch (reason) {
                          setChangeError(
                            reason instanceof Error
                              ? reason.message
                              : "The organization could not be changed.",
                          );
                        }
                      }}
                    >
                      <select name="organizationId" required defaultValue="">
                        <option value="" disabled>
                          {language === "mk"
                            ? "Изберете нова организација"
                            : language === "sq"
                              ? "Zgjidhni organizatën e re"
                              : "Choose the new organization"}
                        </option>
                        {(availableOrganizations.data ?? [])
                          .filter(
                            (candidate) => candidate.id !== organization.id,
                          )
                          .map((candidate) => (
                            <option value={candidate.id} key={candidate.id}>
                              {candidate.name} · {candidate.region}
                            </option>
                          ))}
                      </select>
                      <button className="secondary">
                        {language === "mk"
                          ? "Промени организација"
                          : language === "sq"
                            ? "Ndrysho organizatën"
                            : "Change organization"}
                      </button>
                    </form>
                  )}
                {item.status === "Accepted" &&
                  item.requestType === "Subscription" && (
                    <p className="notice padded">
                      {language === "mk"
                        ? "Промената е одобрена. Администраторот сега може да ви испрати нова покана за претплата."
                        : language === "sq"
                          ? "Ndryshimi u miratua. Administratori tani mund t'ju dërgojë një ftesë të re abonimi."
                          : "The change was approved. The administrator can now send a new subscription invitation."}
                    </p>
                  )}
              </article>
            ))}
          </div>
        )}
        {changeError && <p className="form-error">{changeError}</p>}
      </article>
    </div>
  );
}

function PaymentRow({ label, value }: { label: string; value: string }) {
  if (!value) return null;
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function ProfilePanel({
  profile,
  onChanged,
}: {
  profile: Profile | null;
  onChanged: () => void;
}) {
  const language = usePortalLanguage();
  const t = dashboardCopy[language].client;
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  if (!profile) return <p>{t.profileLoading}</p>;
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await api("/api/profile/", {
        method: "PATCH",
        body: JSON.stringify({
          firstName: data.get("firstName"),
          lastName: data.get("lastName"),
          phone: data.get("phone"),
          preferredLanguage: data.get("preferredLanguage"),
        }),
      });
      setMessage(t.saved);
      onChanged();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.saveError);
    }
  };
  return (
    <form className="workspace workspace-form" onSubmit={submit}>
      <h2>{t.profileSettings}</h2>
      <label>
        {t.email}
        <input value={profile.email} disabled />
      </label>
      <div className="row">
        <label>
          {t.firstName}
          <input name="firstName" defaultValue={profile.firstName} required />
        </label>
        <label>
          {t.lastName}
          <input name="lastName" defaultValue={profile.lastName} required />
        </label>
      </div>
      <label>
        {t.phone}
        <input name="phone" defaultValue={profile.phoneNumber} />
      </label>
      <label>
        {t.language}
        <select
          name="preferredLanguage"
          defaultValue={profile.preferredLanguage}
        >
          <option value="mk">Македонски</option>
          <option value="en">English</option>
          <option value="sq">Shqip</option>
        </select>
      </label>
      {message && <p className="notice">{message}</p>}
      {error && <p className="form-error">{error}</p>}
      <button className="primary">{t.save}</button>
    </form>
  );
}
