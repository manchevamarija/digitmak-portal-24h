import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { api, getAccessToken } from "../../api";
import {
  DocumentPreview,
  type PreviewDocument,
} from "../../components/documents/DocumentPreview";
import { useAuth } from "../../features/auth/useAuth";
import type {
  Meeting,
  Ticket,
  TicketAttachment,
  TicketMessage,
} from "../../shared/domain";
import type { Language, Navigate } from "../../shared/types";
import { useApiResource } from "../../shared/useApiResource";
import {
  labelFor,
  systemEventFor,
  ticketStatusClass,
} from "../../shared/labels";
import { usePortalLanguage } from "../../shared/usePortalLanguage";
import { localeFor } from "../../content/dashboardCopy";
import { workspaceCopy } from "../../content/workspaceCopy";
export type StaffUser = {
  id: string;
  email: string;
  firstName?: string;
  lastName?: string;
  role: string;
};
type Contact = {
  id: string;
  organizationName: string;
  contactName: string;
  email: string;
  dmaCategory: string;
  mainNeed: string;
  status: string;
  linkedOrganizationId?: string;
};
type OrganizationDetail = {
  organization: {
    id: string;
    name: string;
    type: string;
    sector?: string;
    municipality?: string;
    region?: string;
    website?: string;
    employeeCount?: number;
    status: string;
  };
  members: {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    phoneNumber?: string;
    isPrimaryContact: boolean;
  }[];
};
type Tab = "tickets" | "meetings" | "contacts";

export function StaffDashboardPage({
  onNavigate,
  initialTicketId,
}: {
  onNavigate: Navigate;
  initialTicketId?: string;
}) {
  const language = usePortalLanguage();
  const t = workspaceCopy(language);
  const { user } = useAuth();
  const [version, setVersion] = useState(0);
  const [tab, setTab] = useState<Tab>("meetings");
  const [selected, setSelected] = useState<Ticket | null>(null);
  const [openedInitialTicketId, setOpenedInitialTicketId] = useState<string>();
  const [ticketSearch, setTicketSearch] = useState("");
  const [ticketStatus, setTicketStatus] = useState("");
  const [ticketCategory, setTicketCategory] = useState("");
  const [error, setError] = useState("");
  const [editingMeeting, setEditingMeeting] = useState<Meeting | null>(null);
  const [organizationDetail, setOrganizationDetail] =
    useState<OrganizationDetail | null>(null);
  const enabled = !!user?.roles.some((role) =>
    ["Admin", "HelpDeskAgent", "Expert"].includes(role),
  );
  const canTriage = !!user?.roles.some((role) =>
    ["Admin", "HelpDeskAgent"].includes(role),
  );
  const refresh = () => setVersion((value) => value + 1);
  const tickets = useApiResource<Ticket[]>(
    `/api/staff/tickets?pageSize=100&v=${version}`,
    enabled,
  );
  useEffect(() => {
    if (!initialTicketId || openedInitialTicketId === initialTicketId) return;
    const ticket = tickets.data?.find((item) => item.id === initialTicketId);
    if (!ticket) return;
    setTab("tickets");
    setSelected(ticket);
    setOpenedInitialTicketId(initialTicketId);
  }, [initialTicketId, openedInitialTicketId, tickets.data]);
  const meetings = useApiResource<Meeting[]>(
    `/api/staff/meetings?v=${version}`,
    enabled,
  );
  const staff = useApiResource<StaffUser[]>(
    `/api/staff/users?v=${version}`,
    enabled && canTriage,
  );
  const contacts = useApiResource<Contact[]>(
    `/api/staff/contact-requests?pageSize=100&v=${version}`,
    enabled && canTriage && tab === "contacts",
  );
  const loadOrganization = async (id: string) => {
    try {
      setOrganizationDetail(
        await api<OrganizationDetail>(`/api/staff/organizations/${id}`),
      );
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : t.loadOrganizationError,
      );
    }
  };
  const meetingAction = async (
    event: FormEvent<HTMLFormElement>,
    meeting: Meeting,
    action: string,
  ) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      const start = String(data.get("startsAt") ?? "");
      const end = String(data.get("endsAt") ?? "");
      await api(`/api/staff/meetings/${meeting.id}/${action}`, {
        method: "POST",
        body: JSON.stringify({
          startsAt: start ? new Date(start).toISOString() : null,
          endsAt: end ? new Date(end).toISOString() : null,
          location: data.get("location"),
          onlineLink: data.get("onlineLink"),
          notes: data.get("notes"),
          assignedUserId: data.get("assignedUserId") || null,
        }),
      });
      setEditingMeeting(null);
      refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.decisionError);
    }
  };
  const downloadStaffCalendar = async () => {
    const response = await fetch("/api/staff/meetings/calendar.ics", {
      headers: { Authorization: `Bearer ${getAccessToken() ?? ""}` },
    });
    if (!response.ok) return setError(t.reportDownloadError);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = "digitmak-staff-calendar.ics";
    link.click();
    URL.revokeObjectURL(url);
  };
  if (user && !enabled)
    return (
      <section className="page">
        <h1>{t.noStaffAccess}</h1>
        <button className="secondary" onClick={() => onNavigate("dashboard")}>
          {t.back}
        </button>
      </section>
    );
  const tabs: { key: Tab; label: string }[] = [
    { key: "tickets", label: t.triage },
    { key: "meetings", label: t.calendarMeetings },
    ...(canTriage
      ? [{ key: "contacts" as const, label: t.contactRequests }]
      : []),
  ];
  const visibleTickets = (tickets.data ?? []).filter(
    (ticket) =>
      (!ticketSearch ||
        `${ticket.ticketNumber} ${ticket.title} ${ticket.description}`
          .toLocaleLowerCase()
          .includes(ticketSearch.toLocaleLowerCase())) &&
      (!ticketStatus || ticket.status === ticketStatus) &&
      (!ticketCategory || ticket.category === ticketCategory),
  );
  const newTicketsCount = (tickets.data ?? []).filter(
    (ticket) => ticket.status === "New",
  ).length;
  return (
    <section className="dashboard">
      <aside>
        <div className="user">
          <span>HD</span>
          <div>
            <b>{user?.email ?? t.helpDeskAgent}</b>
            <small>{t.staffArea}</small>
          </div>
        </div>
        {tabs.map((item) => (
          <button
            key={item.key}
            className={tab === item.key ? "sel" : ""}
            onClick={() => {
              setTab(item.key);
              setSelected(null);
            }}
          >
            {item.label}
            {item.key === "tickets" && newTicketsCount > 0 && (
              <span className="menu-badge">{newTicketsCount}</span>
            )}
            <span>›</span>
          </button>
        ))}
        <button onClick={() => onNavigate("dashboard")}>
          {t.clientPortal} <span>›</span>
        </button>
        {user?.roles.includes("Admin") && (
          <button onClick={() => onNavigate("admin")}>
            {t.administration} <span>›</span>
          </button>
        )}
      </aside>
      <div className="dash-main">
        <div className="dash-head">
          <div>
            <span>{t.staffWorkspace}</span>
            <h1>{tabs.find((item) => item.key === tab)?.label}</h1>
          </div>
        </div>
        {tab === "tickets" &&
          (selected ? (
            <StaffTicketDetail
              ticket={selected}
              staff={staff.data ?? []}
              onBack={() => setSelected(null)}
              onChanged={() => {
                refresh();
                setSelected(null);
              }}
              onOrganization={() => loadOrganization(selected.organizationId)}
              language={language}
              canAssign={canTriage}
              onManageExperts={
                user?.roles.includes("Admin")
                  ? () => onNavigate("admin", { tab: "users" })
                  : undefined
              }
            />
          ) : (
            <>
              <div className="stats">
                <article>
                  <span>{t.new}</span>
                  <b>
                    {tickets.data?.filter((x) => x.status === "New").length ??
                      0}
                  </b>
                </article>
                <article>
                  <span>{t.inProgress}</span>
                  <b>
                    {tickets.data?.filter((x) => x.status === "InProgress")
                      .length ?? 0}
                  </b>
                </article>
                <article>
                  <span>{t.urgent}</span>
                  <b>
                    {tickets.data?.filter((x) => x.priority === "Urgent")
                      .length ?? 0}
                  </b>
                </article>
              </div>
              <div className="ticket-list">
                <div className="list-head">
                  <h2>{t.ticketsForWork}</h2>
                </div>
                <div className="ticket-filters">
                  <input
                    value={ticketSearch}
                    onChange={(event) => setTicketSearch(event.target.value)}
                    placeholder={
                      language === "mk"
                        ? "Пребарај по број или наслов"
                        : language === "sq"
                          ? "Kërko sipas numrit ose titullit"
                          : "Search by number or title"
                    }
                  />
                  <select
                    value={ticketStatus}
                    onChange={(event) => setTicketStatus(event.target.value)}
                  >
                    <option value="">
                      {language === "mk"
                        ? "Сите статуси"
                        : language === "sq"
                          ? "Të gjitha statuset"
                          : "All statuses"}
                    </option>
                    {[
                      "New",
                      "Assigned",
                      "InProgress",
                      "Resolved",
                      "Closed",
                    ].map((value) => (
                      <option key={value} value={value}>
                        {labelFor(value, language)}
                      </option>
                    ))}
                  </select>
                  <select
                    value={ticketCategory}
                    onChange={(event) => setTicketCategory(event.target.value)}
                  >
                    <option value="">
                      {language === "mk"
                        ? "Сите категории"
                        : language === "sq"
                          ? "Të gjitha kategoritë"
                          : "All categories"}
                    </option>
                    {Array.from(
                      new Set(
                        (tickets.data ?? []).map((ticket) => ticket.category),
                      ),
                    ).map((value) => (
                      <option key={value} value={value}>
                        {labelFor(value, language)}
                      </option>
                    ))}
                  </select>
                </div>
                {visibleTickets.map((ticket) => (
                  <button
                    className="ticket ticket-button"
                    key={ticket.id}
                    onClick={() => setSelected(ticket)}
                  >
                    <span className={`tag ${ticketStatusClass(ticket.status)}`}>
                      {labelFor(ticket.status, language)}
                    </span>
                    <div>
                      <b>{ticket.title}</b>
                      <small>
                        #{ticket.ticketNumber} ·{" "}
                        {labelFor(ticket.category, language)} ·{" "}
                        {labelFor(ticket.priority, language)}
                      </small>
                    </div>
                  </button>
                ))}
                {!visibleTickets.length && (
                  <div className="empty-state">
                    {language === "mk"
                      ? "Нема тикети што одговараат на филтрите."
                      : language === "sq"
                        ? "Nuk ka tiketa që përputhen me filtrat."
                        : "No tickets match the filters."}
                  </div>
                )}
              </div>
            </>
          ))}
        {tab === "meetings" && (
          <div>
            <button
              className="secondary"
              onClick={() => void downloadStaffCalendar()}
            >
              {t.downloadCalendar}
            </button>
            <div className="meeting-summary-grid">
              {(meetings.data ?? []).map((meeting) => (
                <article className="meeting-summary-card" key={meeting.id}>
                  <div className="meeting-summary-head">
                    <span className="tag amber">
                      {labelFor(meeting.status, language)}
                    </span>
                    <span className="meeting-summary-type">
                      {labelFor(meeting.meetingType, language)}
                    </span>
                  </div>
                  <h3>{meeting.subject}</h3>
                  {meeting.description && <p>{meeting.description}</p>}
                  <dl className="meeting-summary-details">
                    <div>
                      <dt>{t.start}</dt>
                      <dd>
                        {meeting.startsAt
                          ? new Intl.DateTimeFormat(localeFor(language), {
                              dateStyle: "medium",
                              timeStyle: "short",
                            }).format(new Date(meeting.startsAt))
                          : "—"}
                      </dd>
                    </div>
                    {(meeting.location || meeting.onlineLink) && (
                      <div>
                        <dt>
                          {meeting.meetingType === "Online"
                            ? t.onlineLink
                            : t.location}
                        </dt>
                        <dd>{meeting.location || meeting.onlineLink}</dd>
                      </div>
                    )}
                  </dl>
                  <button
                    className="secondary meeting-edit-button"
                    onClick={() => setEditingMeeting(meeting)}
                  >
                    {language === "mk"
                      ? "Отвори и уреди"
                      : language === "sq"
                        ? "Hap dhe ndrysho"
                        : "Open and edit"}
                  </button>
                </article>
              ))}
            </div>
            {!meetings.loading && !(meetings.data ?? []).length && (
              <div className="empty-state">{t.noMeetings}</div>
            )}
            {editingMeeting && (
              <div
                className="meeting-edit-overlay"
                role="presentation"
                onMouseDown={(event) => {
                  if (event.target === event.currentTarget) {
                    setEditingMeeting(null);
                  }
                }}
              >
                <section
                  className="workspace meeting-edit-dialog"
                  role="dialog"
                  aria-modal="true"
                  aria-label={editingMeeting.subject}
                  onMouseDown={(event) => event.stopPropagation()}
                >
                  <div className="meeting-edit-heading">
                    <div>
                      <span className="kicker">{t.calendarMeetings}</span>
                      <h2>{editingMeeting.subject}</h2>
                    </div>
                    <button
                      className="secondary meeting-dialog-close"
                      onClick={() => setEditingMeeting(null)}
                    >
                      {language === "mk"
                        ? "Затвори"
                        : language === "sq"
                          ? "Mbyll"
                          : "Close"}
                    </button>
                  </div>
                  <form
                    className="workspace-form meeting-edit-form"
                    onSubmit={(event) => {
                      const submitter = (event.nativeEvent as SubmitEvent)
                        .submitter as HTMLButtonElement | null;
                      void meetingAction(
                        event,
                        editingMeeting,
                        submitter?.value || "confirm",
                      );
                    }}
                  >
                    <div className="row">
                      <label>
                        {t.start}
                        <input
                          name="startsAt"
                          type="datetime-local"
                          defaultValue={editingMeeting.startsAt?.slice(0, 16)}
                        />
                      </label>
                      <label>
                        {t.end}
                        <input
                          name="endsAt"
                          type="datetime-local"
                          defaultValue={editingMeeting.endsAt?.slice(0, 16)}
                        />
                      </label>
                    </div>
                    {canTriage && (
                      <label>
                        {t.assignedAdvisor}
                        <select name="assignedUserId">
                          <option value="">{t.choose}</option>
                          {(staff.data ?? []).map((item) => (
                            <option
                              value={item.id}
                              key={`${item.id}-${item.role}`}
                            >
                              {item.email} · {item.role}
                            </option>
                          ))}
                        </select>
                      </label>
                    )}
                    <label>
                      {t.location}
                      <input
                        name="location"
                        defaultValue={editingMeeting.location}
                      />
                    </label>
                    <label>
                      {t.onlineLink}
                      <input
                        name="onlineLink"
                        type="url"
                        defaultValue={editingMeeting.onlineLink}
                      />
                    </label>
                    <label>
                      {t.notes}
                      <textarea
                        name="notes"
                        defaultValue={editingMeeting.notes}
                      />
                    </label>
                    <div className="action-row">
                      <button
                        className="approve"
                        name="meetingAction"
                        value="confirm"
                      >
                        {t.confirm}
                      </button>
                      <button
                        type="submit"
                        className="secondary"
                        name="meetingAction"
                        value="propose"
                      >
                        {language === "en"
                          ? "Propose another time"
                          : language === "sq"
                            ? "Propozo një orar tjetër"
                            : "Предложи друг термин"}
                      </button>
                      <button
                        type="button"
                        className="reject"
                        onClick={() =>
                          void api(
                            `/api/staff/meetings/${editingMeeting.id}/reject`,
                            { method: "POST", body: "{}" },
                          ).then(() => {
                            setEditingMeeting(null);
                            refresh();
                          })
                        }
                      >
                        {t.reject}
                      </button>
                      {editingMeeting.status === "Confirmed" && (
                        <button
                          type="button"
                          onClick={() =>
                            void api(
                              `/api/staff/meetings/${editingMeeting.id}/complete`,
                              { method: "POST", body: "{}" },
                            ).then(() => {
                              setEditingMeeting(null);
                              refresh();
                            })
                          }
                        >
                          {t.complete}
                        </button>
                      )}
                    </div>
                  </form>
                </section>
              </div>
            )}
          </div>
        )}
        {tab === "contacts" && (
          <div className="ticket-list">
            <div className="list-head">
              <h2>{t.publicDma}</h2>
            </div>
            {(contacts.data ?? []).map((item) => (
              <div className="approval" key={item.id}>
                <div>
                  <b>{item.organizationName}</b>
                  <small>
                    {item.contactName} · {item.email} ·{" "}
                    {labelFor(item.dmaCategory, language)} ·{" "}
                    {labelFor(item.status, language)}
                  </small>
                </div>
                {item.linkedOrganizationId && (
                  <button
                    onClick={() => loadOrganization(item.linkedOrganizationId!)}
                  >
                    {t.organization}
                  </button>
                )}
              </div>
            ))}
            {!contacts.loading && !(contacts.data ?? []).length && (
              <div className="empty-state">
                <h3>{t.noContactRequests}</h3>
                <p>{t.noContactRequestsHelp}</p>
              </div>
            )}
          </div>
        )}
        {organizationDetail && (
          <div className="workspace organization-detail">
            <div className="organization-detail-head">
              <button
                className="back-link"
                onClick={() => setOrganizationDetail(null)}
              >
                × {t.close}
              </button>
              <span className="kicker">{t.clientOrganization}</span>
            </div>
            <h2>{organizationDetail.organization.name}</h2>
            <p>
              {labelFor(organizationDetail.organization.type, language)} ·{" "}
              {organizationDetail.organization.sector ?? "—"} ·{" "}
              {organizationDetail.organization.municipality ??
                organizationDetail.organization.region ??
                "—"}
            </p>
            <p>
              {organizationDetail.organization.employeeCount ?? "—"}{" "}
              {t.employees} ·{" "}
              {labelFor(organizationDetail.organization.status, language)}
            </p>
            {organizationDetail.organization.website && (
              <a
                href={organizationDetail.organization.website}
                target="_blank"
                rel="noreferrer"
              >
                {organizationDetail.organization.website}
              </a>
            )}
            <h3>{t.activeContacts}</h3>
            {organizationDetail.members.map((member) => (
              <div className="approval" key={member.id}>
                <div>
                  <b>
                    {member.firstName} {member.lastName}
                    {member.isPrimaryContact ? ` · ${t.primaryContact}` : ""}
                  </b>
                  <small>
                    {member.email} · {member.phoneNumber ?? "—"}
                  </small>
                </div>
              </div>
            ))}
          </div>
        )}
        {(error || tickets.error || contacts.error) && (
          <p className="form-error">
            {error || tickets.error || contacts.error}
          </p>
        )}
      </div>
    </section>
  );
}

export function StaffTicketDetail({
  ticket,
  staff,
  onBack,
  onChanged,
  onOrganization,
  language,
  canAssign,
  onManageExperts,
}: {
  ticket: Ticket;
  staff: StaffUser[];
  onBack: () => void;
  onChanged: () => void;
  onOrganization: () => void;
  language: Language;
  canAssign: boolean;
  onManageExperts?: () => void;
}) {
  const t = workspaceCopy(language);
  const experts = staff
    .filter((item) => item.role === "Expert")
    .filter(
      (item, index, items) =>
        items.findIndex((candidate) => candidate.id === item.id) === index,
    );
  const [messages, setMessages] = useState<TicketMessage[]>([]);
  const [attachments, setAttachments] = useState<TicketAttachment[]>([]);
  const [preview, setPreview] = useState<PreviewDocument>();
  const [error, setError] = useState("");
  useEffect(() => {
    api<TicketMessage[]>(`/api/tickets/${ticket.id}/messages`)
      .then(setMessages)
      .catch((reason) => setError(reason.message));
    api<TicketAttachment[]>(`/api/tickets/${ticket.id}/attachments`)
      .then(setAttachments)
      .catch((reason) => setError(reason.message));
    const connection = new HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_URL ?? ""}/hubs/tickets`, {
        accessTokenFactory: () => getAccessToken() ?? "",
      })
      .withAutomaticReconnect()
      .build();
    connection.on("TicketMessageCreated", (message: TicketMessage) =>
      setMessages((current) =>
        current.some((x) => x.id === message.id)
          ? current
          : [...current, message],
      ),
    );
    void connection
      .start()
      .then(() => connection.invoke("JoinTicket", ticket.id))
      .catch(() => setError(t.liveDisconnected));
    return () => {
      void connection.stop();
    };
  }, [ticket.id, t.liveDisconnected]);
  const downloadAttachment = async (attachment: TicketAttachment) => {
    const response = await fetch(`/api/files/${attachment.fileId}`, {
      credentials: "include",
      headers: { Authorization: `Bearer ${getAccessToken() ?? ""}` },
    });
    if (!response.ok) return setError(t.attachmentDownloadError);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = attachment.originalFilename;
    link.click();
    URL.revokeObjectURL(url);
  };
  const uploadAttachment = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const file = data.get("file");
    if (!(file instanceof File)) return;
    const payload = new FormData();
    payload.append("file", file);
    const response = await fetch(`/api/tickets/${ticket.id}/attachments`, {
      method: "POST",
      credentials: "include",
      headers: { Authorization: `Bearer ${getAccessToken() ?? ""}` },
      body: payload,
    });
    if (!response.ok) return setError(t.attachmentUploadError);
    setAttachments(
      await api<TicketAttachment[]>(`/api/tickets/${ticket.id}/attachments`),
    );
    form.reset();
    setError("");
  };
  const post = async (
    event: FormEvent<HTMLFormElement>,
    type: "messages" | "internal-notes",
  ) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    try {
      await api(`/api/staff/tickets/${ticket.id}/${type}`, {
        method: "POST",
        body: JSON.stringify({ body: data.get("body") }),
      });
      form.reset();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.sendError);
    }
  };
  const assign = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    await api(`/api/staff/tickets/${ticket.id}/assign`, {
      method: "POST",
      body: JSON.stringify({
        agentId: data.get("agentId") || null,
        expertId: data.get("expertId") || null,
      }),
    });
    onChanged();
  };
  const status = async (value: string) => {
    await api(`/api/staff/tickets/${ticket.id}/status?status=${value}`, {
      method: "PATCH",
    });
    onChanged();
  };
  const resolve = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    await api(`/api/staff/tickets/${ticket.id}/resolve`, {
      method: "POST",
      body: JSON.stringify({
        finalRecommendation: data.get("finalRecommendation"),
        referralRecommendation: data.get("referralRecommendation"),
      }),
    });
    onChanged();
  };
  return (
    <div className="workspace">
      <button className="back-link" onClick={onBack}>
        ← {t.back}
      </button>
      <div className="workspace-head">
        <div>
          <span>#{ticket.ticketNumber}</span>
          <h2>{ticket.title}</h2>
          <p>{ticket.description}</p>
        </div>
        <div className="ticket-head-actions">
          <span className={`tag ${ticketStatusClass(ticket.status)}`}>
            {labelFor(ticket.status, language)}
          </span>
          <button className="secondary" onClick={onOrganization}>
            {t.organization}
          </button>
        </div>
      </div>
      {canAssign && (
        <form className="workspace-form" onSubmit={assign}>
          <h3>{t.assignment}</h3>
          <div className="row">
            <label>
              {t.helpDeskAgent}
              <select name="agentId" defaultValue={ticket.assignedAgentId ?? ""}>
                <option value="">{t.choose}</option>
                {staff
                  .filter((x) => ["HelpDeskAgent", "Admin"].includes(x.role))
                  .filter(
                    (item, index, items) =>
                      items.findIndex(
                        (candidate) => candidate.id === item.id,
                      ) === index,
                  )
                  .map((item) => (
                    <option key={`${item.id}-${item.role}`} value={item.id}>
                      {item.email}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              {t.expert}
              <select name="expertId" disabled={!experts.length} defaultValue={ticket.assignedExpertId ?? ""}>
                <option value="">{t.noExpert}</option>
                {experts.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.email}
                  </option>
                ))}
              </select>
              {!experts.length && (
                <>
                  <small>
                    {language === "mk"
                      ? "Нема активен корисник со улога Експерт. Доделувањето експерт е опционално."
                      : "There is no active user with the Expert role. Expert assignment is optional."}
                  </small>
                  {onManageExperts && (
                    <button
                      type="button"
                      className="secondary compact-action"
                      onClick={onManageExperts}
                    >
                      {language === "mk" ? "Додај експерт" : "Add expert"}
                    </button>
                  )}
                </>
              )}
            </label>
          </div>
          <button className="primary">{t.assign}</button>
        </form>
      )}
      <div className="action-row">
        <button onClick={() => status("InProgress")}>{t.begin}</button>
        <button onClick={() => status("Closed")}>{t.close}</button>
      </div>
      <div className="chat">
        {messages.map((message) => (
          <article
            key={message.id}
            className={message.messageType === "InternalNote" ? "internal" : ""}
          >
            <small>{labelFor(message.messageType, language)}</small>
            <p>
              {message.messageType === "SystemEvent"
                ? systemEventFor(message.body, language)
                : message.body}
            </p>
            <time>
              {new Date(message.createdAt).toLocaleString(localeFor(language))}
            </time>
          </article>
        ))}
      </div>
      <section className="detail-card ticket-attachments">
        <div className="list-head">
          <div>
            <h3>{t.attachments}</h3>
            <p>{t.attachmentsHelp}</p>
          </div>
          <span className="tag blue">{attachments.length}</span>
        </div>
        {attachments.map((attachment) => (
          <div className="approval" key={attachment.id}>
            <div>
              <b>{attachment.originalFilename}</b>
              <small>
                {Math.ceil(attachment.sizeBytes / 1024)} KB ·{" "}
                {new Date(attachment.createdAt).toLocaleString(
                  localeFor(language),
                )}
              </small>
            </div>
            <button
              type="button"
              className="secondary"
              onClick={() => setPreview(attachment)}
            >
              {t.preview}
            </button>
            <button
              type="button"
              onClick={() => void downloadAttachment(attachment)}
            >
              {t.download}
            </button>
          </div>
        ))}
        {!attachments.length && (
          <p className="empty-state">{t.noAttachments}</p>
        )}
        <form className="inline-form upload" onSubmit={uploadAttachment}>
          <input
            name="file"
            required
            type="file"
            accept=".pdf,.png,.jpg,.jpeg,.txt,.csv,.docx"
          />
          <button className="secondary">{t.attachDocument}</button>
        </form>
      </section>
      {preview && (
        <DocumentPreview
          document={preview}
          onClose={() => setPreview(undefined)}
        />
      )}
      <form
        className="ticket-reply-form workspace-form"
        onSubmit={(event) => post(event, "messages")}
      >
        <label>
          {t.replyClient}
          <textarea name="body" rows={4} required />
        </label>
        <div className="ticket-reply-actions">
          <button className="primary" type="submit">
            {t.send}
          </button>
        </div>
      </form>
      <form
        className="inline-form"
        onSubmit={(event) => post(event, "internal-notes")}
      >
        <input name="body" required placeholder={t.internalNote} />
        <button className="secondary">{t.saveNote}</button>
      </form>
      <form className="workspace-form" onSubmit={resolve}>
        <h3>{t.finalRecommendation}</h3>
        <label>
          {t.practicalRecommendation}
          <textarea name="finalRecommendation" rows={4} required />
        </label>
        <label>
          {t.referralRecommendation}
          <textarea name="referralRecommendation" rows={2} />
        </label>
        <button className="approve">{t.resolveTicket}</button>
      </form>
      {error && <p className="form-error">{error}</p>}
    </div>
  );
}
