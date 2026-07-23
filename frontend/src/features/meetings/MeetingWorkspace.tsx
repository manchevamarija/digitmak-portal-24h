import type { FormEvent } from "react";
import { useState } from "react";
import { api, getAccessToken } from "../../api";
import { dashboardCopy, localeFor } from "../../content/dashboardCopy";
import type { Meeting } from "../../shared/domain";
import { labelFor } from "../../shared/labels";
import { useApiResource } from "../../shared/useApiResource";
import { usePortalLanguage } from "../../shared/usePortalLanguage";

type AdminClient = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organizationId?: string;
  status: string;
  roles: string[];
};

type AdminOrganization = {
  id: string;
  name: string;
  status: string;
};

export function MeetingWorkspace({
  meetings,
  onChanged,
  adminMode = false,
}: {
  meetings: Meeting[];
  onChanged: () => void;
  adminMode?: boolean;
}) {
  const language = usePortalLanguage();
  const t = dashboardCopy[language].meeting;
  const isAdmin = adminMode;
  const adminClients = useApiResource<AdminClient[]>(
    "/api/admin/users",
    isAdmin,
  );
  const adminOrganizations = useApiResource<AdminOrganization[]>(
    "/api/admin/organizations",
    isAdmin,
  );
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [editingMeetingId, setEditingMeetingId] = useState<string>();
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    setError("");
    setSuccess("");
    try {
      const start = String(data.get("preferredStart"));
      const end = String(data.get("preferredEnd"));
      const meeting = {
        subject: data.get("subject"),
        description: data.get("description"),
        meetingType: data.get("meetingType"),
        preferredStart: start ? new Date(start).toISOString() : null,
        preferredEnd: end ? new Date(end).toISOString() : null,
        requestedTimeWindow: data.get("requestedTimeWindow"),
        location: data.get("location"),
        notes: data.get("notes"),
      };
      const clientUserId = String(data.get("clientUserId") ?? "");
      await api(isAdmin ? "/api/admin/meetings" : "/api/meetings/", {
        method: "POST",
        body: JSON.stringify(
          isAdmin ? { userId: clientUserId, meeting } : meeting,
        ),
      });
      if (isAdmin) {
        const client = (adminClients.data ?? []).find(
          (item) => item.id === clientUserId,
        );
        const clientLabel = client
          ? `${client.firstName} ${client.lastName}`.trim() || client.email
          : clientUserId;
        setSuccess(
          language === "en"
            ? `Meeting scheduled for ${clientLabel}.`
            : language === "sq"
              ? `Takimi u caktua për ${clientLabel}.`
              : `Состанокот е закажан за ${clientLabel}.`,
        );
      }
      form.reset();
      onChanged();
    } catch (value) {
      setError(value instanceof Error ? value.message : t.requestError);
    }
  };
  const cancel = async (id: string) => {
    try {
      await api(`/api/meetings/${id}/cancel`, { method: "POST" });
      onChanged();
    } catch (value) {
      setError(value instanceof Error ? value.message : t.cancelError);
    }
  };
  const requestReschedule = async (
    event: FormEvent<HTMLFormElement>,
    id: string,
  ) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const preferredStart = String(data.get("preferredStart") ?? "");
    const preferredEnd = String(data.get("preferredEnd") ?? "");
    try {
      await api(`/api/meetings/${id}/reschedule`, {
        method: "POST",
        body: JSON.stringify({
          preferredStart: preferredStart
            ? new Date(preferredStart).toISOString()
            : null,
          preferredEnd: preferredEnd
            ? new Date(preferredEnd).toISOString()
            : null,
          requestedTimeWindow: data.get("requestedTimeWindow"),
          notes: data.get("notes"),
        }),
      });
      setEditingMeetingId(undefined);
      setError("");
      onChanged();
    } catch (value) {
      setError(value instanceof Error ? value.message : t.requestError);
    }
  };
  const downloadCalendar = async () => {
    const token = getAccessToken();
    const response = await fetch("/api/meetings/calendar.ics", {
      credentials: "include",
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (!response.ok) return setError(t.requestError);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = "digitmak-meetings.ics";
    link.click();
    URL.revokeObjectURL(url);
  };
  const downloadSingleMeeting = (meeting: Meeting) => {
    if (!meeting.startsAt) return;
    const formatDate = (value: string | Date) =>
      new Date(value)
        .toISOString()
        .replaceAll("-", "")
        .replaceAll(":", "")
        .replace(/\.\d{3}Z$/, "Z");
    const escape = (value: string) =>
      value
        .replaceAll("\\", "\\\\")
        .replaceAll(";", "\\;")
        .replaceAll(",", "\\,")
        .replaceAll("\n", "\\n");
    const start = new Date(meeting.startsAt);
    const end = meeting.endsAt
      ? new Date(meeting.endsAt)
      : new Date(start.getTime() + 60 * 60 * 1000);
    const location =
      meeting.meetingType === "Online"
        ? meeting.onlineLink || "Online"
        : meeting.location || "";
    const ics = [
      "BEGIN:VCALENDAR",
      "VERSION:2.0",
      "PRODID:-//DigitMak//Portal V1//MK",
      "CALSCALE:GREGORIAN",
      "METHOD:PUBLISH",
      "BEGIN:VEVENT",
      `UID:${meeting.id}@portal.digitmak.mk`,
      `DTSTAMP:${formatDate(new Date())}`,
      `DTSTART:${formatDate(start)}`,
      `DTEND:${formatDate(end)}`,
      `SUMMARY:${escape(meeting.subject)}`,
      `DESCRIPTION:${escape(meeting.description)}`,
      `LOCATION:${escape(location)}`,
      `STATUS:${meeting.status === "Cancelled" ? "CANCELLED" : "CONFIRMED"}`,
      "END:VEVENT",
      "END:VCALENDAR",
      "",
    ].join("\r\n");
    const url = URL.createObjectURL(
      new Blob([ics], { type: "text/calendar;charset=utf-8" }),
    );
    const link = document.createElement("a");
    link.href = url;
    link.download = `digitmak-${meeting.id}.ics`;
    link.click();
    URL.revokeObjectURL(url);
  };
  return (
    <div className="workspace two-column">
      <form className="workspace-form" onSubmit={submit}>
        <h2>
          {isAdmin
            ? language === "mk"
              ? "Закажи состанок со клиент"
              : language === "sq"
                ? "Cakto takim me klient"
                : "Schedule a client meeting"
            : t.request}
        </h2>
        {isAdmin && (
          <label>
            {language === "mk"
              ? "Клиент и организација"
              : language === "sq"
                ? "Klienti dhe organizata"
                : "Client and organization"}
            <select name="clientUserId" required defaultValue="">
              <option value="">
                {language === "mk" ? "Избери клиент" : "Select client"}
              </option>
              {(adminClients.data ?? [])
                .filter(
                  (client) =>
                    client.status === "Active" &&
                    client.roles.includes("Client") &&
                    !client.roles.includes("Admin") &&
                    client.organizationId,
                )
                .map((client) => {
                  const organization = (adminOrganizations.data ?? []).find(
                    (item) => item.id === client.organizationId,
                  );
                  return (
                    <option value={client.id} key={client.id}>
                      {`${client.firstName} ${client.lastName}`.trim() ||
                        client.email}
                      {` · ${client.email}`}
                      {organization ? ` · ${organization.name}` : ""}
                    </option>
                  );
                })}
            </select>
          </label>
        )}
        <label>
          {t.subject}
          <input name="subject" required />
        </label>
        <label>
          {t.description}
          <textarea name="description" rows={4} required />
        </label>
        <div className="row">
          <label>
            {t.type}
            <select name="meetingType">
              <option value="Online">{t.online}</option>
              <option value="Onsite">{t.onsite}</option>
            </select>
          </label>
          <label>
            {t.preferred}
            <input name="preferredStart" type="datetime-local" />
          </label>
        </div>
        <div className="row">
          <label>
            {t.end}
            <input name="preferredEnd" type="datetime-local" />
          </label>
          <label>
            {t.alternative}
            <input name="requestedTimeWindow" placeholder={t.alternativeHint} />
          </label>
        </div>
        <label>
          {t.locationInput}
          <input name="location" />
        </label>
        <label>
          {t.notes}
          <textarea name="notes" rows={2} />
        </label>
        <button className="primary">{t.submit}</button>
        {error && <p className="form-error">{error}</p>}
        {success && <p className="form-success">{success}</p>}
      </form>
      <div>
        <div className="list-head">
          <div>
            <h2>
              {isAdmin
                ? language === "en"
                  ? "Meetings you've scheduled for clients"
                  : language === "sq"
                    ? "Takimet që keni caktuar për klientë"
                    : "Состаноци што сте ги закажале за клиенти"
                : t.my}
            </h2>
            <small className="calendar-help">{t.calendarHelp}</small>
          </div>
          <button
            className="secondary"
            type="button"
            onClick={downloadCalendar}
          >
            {t.calendar}
          </button>
        </div>
        {isAdmin && (
          <p className="notice">
            {language === "en" ? (
              <>
                Waiting for a client's meeting request?{" "}
                <a href="/staff?tab=meetings">Open the client meetings queue</a>{" "}
                to confirm, reject or propose a new time.
              </>
            ) : language === "sq" ? (
              <>
                Prisni një kërkesë takimi nga klienti?{" "}
                <a href="/staff?tab=meetings">Hapni radhën e takimeve të klientëve</a>{" "}
                për ta konfirmuar, refuzuar ose propozuar një kohë tjetër.
              </>
            ) : (
              <>
                Чекате барање за состанок од клиент?{" "}
                <a href="/staff?tab=meetings">Отворете ги клиентските состаноци</a>{" "}
                за да го потврдите, одбиете или предложите нов термин.
              </>
            )}
          </p>
        )}
        {meetings.map((meeting) => (
          <article className="meeting-card" key={meeting.id}>
            <span className="tag amber">
              {labelFor(meeting.status, language)}
            </span>
            {!isAdmin &&
              meeting.createdByUserId &&
              meeting.createdByUserId !== meeting.requestedByUserId && (
                <span className="tag blue">
                  {language === "en"
                    ? "Scheduled by administrator"
                    : language === "sq"
                      ? "Caktuar nga administratori"
                      : "Закажано од администраторот"}
                </span>
              )}
            <h3>{meeting.subject}</h3>
            <p>{meeting.description}</p>
            <small>
              {meeting.startsAt
                ? new Date(meeting.startsAt).toLocaleString(localeFor(language))
                : t.pending}{" "}
              · {labelFor(meeting.meetingType, language)}
            </small>
            {meeting.location && (
              <p>
                {t.location}: {meeting.location}
              </p>
            )}
            {meeting.onlineLink && (
              <p>
                <a href={meeting.onlineLink} target="_blank" rel="noreferrer">
                  {t.openOnline}
                </a>
              </p>
            )}
            {meeting.notes && <p>{meeting.notes}</p>}
            {meeting.startsAt && (
              <button
                className="secondary"
                type="button"
                onClick={() => downloadSingleMeeting(meeting)}
              >
                {language === "mk"
                  ? "Додај во Outlook / календар"
                  : language === "sq"
                    ? "Shto në Outlook / kalendar"
                    : "Add to Outlook / calendar"}
              </button>
            )}
            {meeting.status !== "Completed" && (
              <button
                className="secondary"
                type="button"
                onClick={() => setEditingMeetingId(meeting.id)}
              >
                {meeting.status === "Cancelled" || meeting.status === "Rejected"
                  ? language === "mk"
                    ? "Побарај повторно закажување"
                    : language === "sq"
                      ? "Kërko ricaktim"
                      : "Request rescheduling"
                  : language === "mk"
                    ? "Побарај промена"
                    : language === "sq"
                      ? "Kërko ndryshim"
                      : "Request a change"}
              </button>
            )}
            {!["Completed", "Cancelled", "Rejected"].includes(
              meeting.status,
            ) && (
              <button className="reject" onClick={() => cancel(meeting.id)}>
                {t.cancel}
              </button>
            )}
            {editingMeetingId === meeting.id && (
              <form
                className="meeting-change-form"
                onSubmit={(event) => requestReschedule(event, meeting.id)}
              >
                <h4>
                  {language === "mk"
                    ? "Предложете нов термин"
                    : language === "sq"
                      ? "Propozoni një termin të ri"
                      : "Propose a new time"}
                </h4>
                <div className="row">
                  <label>
                    {language === "mk" ? "Почеток" : "Start"}
                    <input name="preferredStart" type="datetime-local" />
                  </label>
                  <label>
                    {language === "mk" ? "Крај" : "End"}
                    <input name="preferredEnd" type="datetime-local" />
                  </label>
                </div>
                <label>
                  {language === "mk"
                    ? "Алтернативен период"
                    : "Alternative time window"}
                  <input name="requestedTimeWindow" />
                </label>
                <label>
                  {language === "mk" ? "Забелешка" : "Note"}
                  <textarea name="notes" rows={3} required />
                </label>
                <div className="action-row">
                  <button className="primary">
                    {language === "mk" ? "Испрати барање" : "Send request"}
                  </button>
                  <button
                    type="button"
                    className="secondary"
                    onClick={() => setEditingMeetingId(undefined)}
                  >
                    {language === "mk" ? "Откажи" : "Cancel"}
                  </button>
                </div>
              </form>
            )}
          </article>
        ))}
        {!meetings.length && <p className="empty-state">{t.empty}</p>}
      </div>
    </div>
  );
}
