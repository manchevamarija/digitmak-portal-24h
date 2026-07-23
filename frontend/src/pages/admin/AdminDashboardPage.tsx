import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { api, ApiError, getAccessToken } from "../../api";
import {
  DocumentPreview,
  type PreviewDocument,
} from "../../components/documents/DocumentPreview";
import { NotificationsPopup } from "../../components/layout/NotificationsPopup";
import { useAuth } from "../../features/auth/useAuth";
import type { Language, Navigate } from "../../shared/types";
import { useApiResource } from "../../shared/useApiResource";
import { labelFor, ticketStatusClass } from "../../shared/labels";
import { usePortalLanguage } from "../../shared/usePortalLanguage";
import { localeFor } from "../../content/dashboardCopy";
import { workspaceCopy } from "../../content/workspaceCopy";
import { uiCopy } from "../../content/uiCopy";
import { StaffTicketDetail } from "../staff/StaffDashboardPage";

type Kpis = {
  activeSubscriptions: number;
  expiredSubscriptions: number;
  aiHelpDeskSubscriptions: number;
  tickets: number;
  newTickets: number;
  meetings: number;
  confirmedMeetings: number;
  contactRequests: number;
  publicInstitutions: number;
  aiActRequests: number;
  referrals: number;
};
type Organization = {
  id: string;
  name: string;
  type: string;
  region?: string;
  status: string;
};
type User = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organizationId?: string;
  status: "PendingVerification" | "Active" | "Inactive";
  emailVerifiedAt?: string;
  preferredLanguage: string;
  phoneNumber?: string;
  createdAt: string;
  updatedAt: string;
  roles: string[];
};
type Subscription = {
  id: string;
  userId: string;
  organizationId: string;
  status: string;
  expiresAt?: string;
};
type SubscriptionInvitation = {
  id: string;
  userId: string;
  organizationId: string;
  status: string;
  expiresAt: string;
  createdAt: string;
};
type AccountChangeRequest = {
  id: string;
  userId: string;
  organizationId: string;
  requestType: "Organization" | "Subscription";
  details: string;
  status: "Pending" | "Accepted" | "Declined";
  decisionNote?: string;
  createdAt: string;
  email: string;
  firstName: string;
  lastName: string;
};
type StaffUser = { id: string; email: string; role: string };
type AdminNotification = {
  id: string;
  recipientUserId?: string;
  recipientEmail?: string;
  type: string;
  language?: string;
  subject: string;
  status: string;
  attemptCount: number;
  nextAttemptAt?: string;
  lastError?: string;
  sentAt?: string;
  createdAt: string;
};
type MyNotification = {
  id: string;
  type: string;
  subject: string;
  body: string;
  actionUrl?: string | null;
  isRead: boolean;
  createdAt: string;
};
type Contact = {
  id: string;
  organizationName: string;
  organizationType: string;
  contactName: string;
  email: string;
  dmaCategory: string;
  mainNeed: string;
  status: string;
  assignedTo?: string;
  linkedOrganizationId?: string;
  sector?: string;
  municipality?: string;
  region?: string;
  website?: string;
  phone?: string;
  preferredLanguage: string;
  employeeCount?: number;
  digitalMaturityRating?: number;
  challengeDescription: string;
  currentTools?: string;
  currentDataSources?: string;
  usesAi?: boolean;
  aiUseCase?: string;
  privacyConcerns?: string;
  interestedInAiActGuidance: boolean;
  trainingNeeds?: string;
  desiredTimeline?: string;
  preferredConsultationFormat?: string;
  consentToContact: boolean;
  privacyPolicyAccepted: boolean;
  createdAt: string;
};
type Audit = {
  id: string;
  action: string;
  entityType: string;
  entityId: string;
  createdAt: string;
};
type Setting = { id: string; key: string; value: string; description?: string };
type ContentItem = {
  id: string;
  slug: string;
  status: string;
  category?: string;
  createdAt: string;
  updatedAt: string;
};
type ContentTranslation = {
  entityId: string;
  language: "mk" | "en" | "sq";
  fieldName: string;
  value: string;
};
type ContentCollection = {
  items: ContentItem[];
  translations: ContentTranslation[];
};
type Ticket = {
  id: string;
  ticketNumber: string;
  organizationId: string;
  title: string;
  description: string;
  category: string;
  priority: string;
  status: string;
  assignedAgentId?: string;
  assignedExpertId?: string;
  createdAt: string;
  updatedAt: string;
  finalRecommendation?: string;
  referralRecommendation?: string;
};
type AdminAttachment = {
  id: string;
  ticketId: string;
  messageId?: string;
  fileId: string;
  uploadedBy: string;
  originalFilename: string;
  contentType: string;
  sizeBytes: number;
  checksum: string;
  ticketNumber: string;
  ticketTitle: string;
  organizationName: string;
  createdAt: string;
};
type Evidence = {
  id: string;
  relatedEntityType: string;
  relatedEntityId: string;
  fileId: string;
  kpiCategory?: string;
  reportingPeriod?: string;
  templateType?: string;
  createdAt: string;
};
type EvidenceTarget = { id: string; label: string };
type CountGroup = { key: string; count: number };
type ContactReport = {
  byOrganizationType: CountGroup[];
  bySector: CountGroup[];
  byRegion: CountGroup[];
  byNeed: CountGroup[];
  byDmaCategory: CountGroup[];
};
type TicketReport = {
  byCategory: CountGroup[];
  byStatus: CountGroup[];
  byAssignee: CountGroup[];
  byOrganizationType: CountGroup[];
};
type MeetingReport = { byStatus: CountGroup[]; byType: CountGroup[] };
type EvidenceTemplate = {
  id: string;
  code: string;
  name: string;
  relatedEntityType: string;
  description?: string;
  requiredMetadataJson: string;
  isActive: boolean;
};

const evidenceTemplateMk: Record<
  string,
  { title: string; description: string }
> = {
  "TICKET-RESOLUTION": {
    title: "Затворање тикет и конечна препорака",
    description:
      "Образец за документирање на дадената поддршка и конечниот исход од тикетот.",
  },
  "MEETING-DELIVERY": {
    title: "Одржан консултативен состанок",
    description:
      "Евиденција за потврден или завршен состанок, учесници и договорени резултати.",
  },
  "SUBSCRIPTION-KPI": {
    title: "Активна годишна претплата",
    description:
      "Образец за евиденција на активирана лична претплата и периодот на важност.",
  },
  "CONTACT-INTAKE": {
    title: "Прием и обработка на контакт-барање",
    description:
      "Евиденција за организацијата, потребата и начинот на кој е обработено јавното барање.",
  },
  "KPI-PERIOD": {
    title: "KPI досие за извештаен период",
    description:
      "Основен образец за показател, вредност, извор и одобрување во избраниот период.",
  },
  "KPI-CONTACT-BREAKDOWN": {
    title: "Преглед на контакт-барања",
    description:
      "Контактите групирани по сектор, регион, тип на организација и DMA потреба.",
  },
  "KPI-TICKET-BREAKDOWN": {
    title: "Преглед на тикети за поддршка",
    description:
      "Тикетите групирани по категорија, статус, приоритет, одговорно лице и организација.",
  },
  "KPI-MEETING-REFERRAL": {
    title: "Состаноци и упатувања",
    description:
      "Завршени консултации и упатувања подготвени за програмско известување.",
  },
  "KPI-SUBSCRIPTION-COHORT": {
    title: "Преглед на претплати",
    description:
      "Поканети, активирани, истечени и откажани претплати за избраниот период.",
  },
};

const evidenceTemplateView = (item: EvidenceTemplate, language: Language) =>
  language === "mk" && evidenceTemplateMk[item.code]
    ? evidenceTemplateMk[item.code]
    : { title: item.name, description: item.description ?? "" };
type OrgDetail = {
  organization: Organization;
  members: {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    memberStatus: string;
  }[];
};
export type Tab =
  | "overview"
  | "myNotifications"
  | "organizations"
  | "changes"
  | "subscriptions"
  | "contacts"
  | "tickets"
  | "documents"
  | "users"
  | "content"
  | "reports"
  | "evidence"
  | "settings"
  | "notifications"
  | "audit";

export function AdminDashboardPage({
  onNavigate,
  initialTab,
  initialTicketId,
  initialOrganizationId,
}: {
  onNavigate: Navigate;
  initialTab?: Tab;
  initialTicketId?: string;
  initialOrganizationId?: string;
}) {
  const language = usePortalLanguage();
  const t = workspaceCopy(language);
  const dma = uiCopy[language].dma;
  const { user } = useAuth();
  const allowed = !!user?.roles.includes("Admin");
  const [version, setVersion] = useState(0);
  const [tab, setTab] = useState<Tab>(initialTab ?? "overview");
  const [scopedError, setScopedError] = useState<{
    tab: Tab;
    message: string;
  } | null>(null);
  const [scopedSuccess, setScopedSuccess] = useState<{
    tab: Tab;
    message: string;
  } | null>(null);
  const [retryingNotificationId, setRetryingNotificationId] =
    useState<string>();
  const setError = (message: string) => setScopedError({ tab, message });
  const [orgDetail, setOrgDetail] = useState<OrgDetail | null>(null);
  const [contactDetail, setContactDetail] = useState<Contact | null>(null);
  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [preview, setPreview] = useState<PreviewDocument>();
  const [openedInitialTicketId, setOpenedInitialTicketId] = useState<string>();
  const [creatingTicket, setCreatingTicket] = useState(false);
  const [selectedClientId, setSelectedClientId] = useState("");
  const [selectedSubscriptionUserId, setSelectedSubscriptionUserId] =
    useState("");
  const [ticketSearch, setTicketSearch] = useState("");
  const [ticketStatus, setTicketStatus] = useState("");
  const [ticketCategory, setTicketCategory] = useState("");
  const [evidenceView, setEvidenceView] = useState<
    "upload" | "templates" | "register"
  >("upload");
  const [evidenceEntityType, setEvidenceEntityType] = useState("Ticket");
  const [evidenceRelatedId, setEvidenceRelatedId] = useState("");
  const [showEvidenceTargets, setShowEvidenceTargets] = useState(false);
  const refresh = () => setVersion((value) => value + 1);
  const kpis = useApiResource<Kpis>(
    `/api/admin/reports/kpis?v=${version}`,
    allowed,
  );
  const organizations = useApiResource<Organization[]>(
    `/api/admin/organizations?v=${version}`,
    allowed,
  );
  const users = useApiResource<User[]>(
    `/api/admin/users?v=${version}`,
    allowed,
  );
  const subscriptions = useApiResource<Subscription[]>(
    `/api/admin/subscriptions?v=${version}`,
    allowed,
  );
  const invitations = useApiResource<SubscriptionInvitation[]>(
    `/api/admin/subscription-invitations?v=${version}`,
    allowed && tab === "subscriptions",
  );
  const accountChanges = useApiResource<AccountChangeRequest[]>(
    `/api/admin/account-change-requests?v=${version}`,
    allowed && tab === "changes",
  );
  const staffUsers = useApiResource<StaffUser[]>(
    `/api/staff/users?v=${version}`,
    allowed && (tab === "contacts" || tab === "tickets"),
  );
  const notifications = useApiResource<AdminNotification[]>(
    `/api/admin/notifications?v=${version}`,
    allowed && tab === "notifications",
  );
  const myNotifications = useApiResource<MyNotification[]>(
    `/api/notifications/mine?v=${version}`,
    allowed,
  );
  const unreadMyNotifications = (myNotifications.data ?? []).filter(
    (item) => !item.isRead,
  ).length;
  const [notificationsPopupOpen, setNotificationsPopupOpen] = useState(false);
  const openMyNotification = async (item: MyNotification) => {
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
  const markAllMyNotificationsRead = async () => {
    try {
      await api("/api/notifications/read-all", { method: "POST" });
      refresh();
    } catch {
      // Non-fatal — the popup simply keeps showing the previous read state.
    }
  };
  const deleteMyNotification = async (item: MyNotification) => {
    try {
      await api(`/api/notifications/${item.id}`, { method: "DELETE" });
      refresh();
    } catch {
      // Non-fatal — the item simply stays in the list if the delete failed.
    }
  };
  const contacts = useApiResource<Contact[]>(
    `/api/admin/contact-requests?v=${version}`,
    allowed && tab === "contacts",
  );
  const serviceContent = useApiResource<ContentCollection>(
    `/api/admin/services?v=${version}`,
    allowed && tab === "content",
  );
  const pageContent = useApiResource<ContentCollection>(
    `/api/admin/pages?v=${version}`,
    allowed && tab === "content",
  );
  const audits = useApiResource<Audit[]>(
    `/api/admin/audit-logs?v=${version}`,
    allowed && tab === "audit",
  );
  const settings = useApiResource<Setting[]>(
    `/api/admin/settings?v=${version}`,
    allowed && (tab === "settings" || tab === "subscriptions"),
  );
  const tickets = useApiResource<Ticket[]>(
    `/api/staff/tickets?v=${version}`,
    allowed && tab === "tickets",
  );
  useEffect(() => {
    if (!initialTicketId || openedInitialTicketId === initialTicketId) return;
    const ticket = tickets.data?.find((item) => item.id === initialTicketId);
    if (!ticket) return;
    setTab("tickets");
    setSelectedTicket(ticket);
    setOpenedInitialTicketId(initialTicketId);
  }, [initialTicketId, openedInitialTicketId, tickets.data]);
  const attachments = useApiResource<AdminAttachment[]>(
    `/api/admin/ticket-attachments?v=${version}`,
    allowed && tab === "documents",
  );
  const evidence = useApiResource<Evidence[]>(
    `/api/admin/evidence?v=${version}`,
    allowed && tab === "evidence",
  );
  const evidenceTemplates = useApiResource<EvidenceTemplate[]>(
    `/api/admin/evidence-templates?v=${version}`,
    allowed && tab === "evidence",
  );
  const evidenceTargets = useApiResource<EvidenceTarget[]>(
    `/api/admin/evidence-targets?type=${encodeURIComponent(evidenceEntityType)}&v=${version}`,
    allowed && tab === "evidence" && showEvidenceTargets,
  );
  const contactReport = useApiResource<ContactReport>(
    `/api/admin/reports/contacts?v=${version}`,
    allowed && tab === "reports",
  );
  const ticketReport = useApiResource<TicketReport>(
    `/api/admin/reports/tickets-detailed?v=${version}`,
    allowed && tab === "reports",
  );
  const meetingReport = useApiResource<MeetingReport>(
    `/api/admin/reports/meetings?v=${version}`,
    allowed && tab === "reports",
  );
  const referralReport = useApiResource<CountGroup[]>(
    `/api/admin/reports/referrals?v=${version}`,
    allowed && tab === "reports",
  );
  const call = async (
    path: string,
    options: RequestInit = { method: "POST" },
    successMessage?: string,
  ) => {
    try {
      await api(path, options);
      setScopedError(null);
      setScopedSuccess(
        successMessage ? { tab, message: successMessage } : null,
      );
      refresh();
      return true;
    } catch (reason) {
      const activeSubscription =
        language === "en"
          ? "This user already has an active subscription. No new invitation is needed."
          : language === "sq"
            ? "Ky përdorues tashmë ka një abonim aktiv. Nuk nevojitet ftesë e re."
            : "Овој корисник веќе има активна претплата. Не е потребна нова покана.";
      const organizationMismatch =
        language === "mk"
          ? "Изберете ја одобрената организација на која припаѓа корисникот."
          : language === "sq"
            ? "Zgjidhni organizatën e miratuar të lidhur me këtë përdorues."
            : "Select the approved organization assigned to this user.";
      const evidenceTemplateMismatch =
        language === "mk"
          ? "Избраниот образец не одговара на поврзаниот тип. Изберете образец од прикажаната листа."
          : language === "sq"
            ? "Modeli i zgjedhur nuk përputhet me llojin e lidhur. Zgjidhni model nga lista."
            : "The selected template does not match the related type. Choose a template from the displayed list.";
      setError(
        reason instanceof ApiError &&
          tab === "subscriptions" &&
          reason.status === 409
          ? activeSubscription
          : reason instanceof ApiError &&
              tab === "subscriptions" &&
              reason.status === 400
            ? organizationMismatch
            : reason instanceof ApiError &&
                tab === "evidence" &&
                reason.message.includes("template does not match")
              ? evidenceTemplateMismatch
              : reason instanceof Error
                ? reason.message
                : t.actionError,
      );
      setScopedSuccess(null);
      return false;
    }
  };
  const retryNotification = async (id: string) => {
    setRetryingNotificationId(id);
    const message =
      language === "en"
        ? "Retry queued. Delivery will start when SMTP is configured."
        : language === "sq"
          ? "Riprovimi u vendos në radhë. Dërgimi do të fillojë kur të konfigurohet SMTP."
          : "Повторното испраќање е закажано. Испораката ќе започне кога ќе се конфигурира SMTP.";
    await call(
      `/api/admin/notifications/${id}/retry`,
      { method: "POST" },
      message,
    );
    setRetryingNotificationId(undefined);
  };
  const orgAction = (id: string, action: string) =>
    call(`/api/admin/organizations/${id}/${action}`);
  const orgMembers = async (id: string) => {
    try {
      setOrgDetail(await api<OrgDetail>(`/api/admin/organizations/${id}`));
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : t.loadOrganizationError,
      );
    }
  };
  const [openedInitialOrganizationId, setOpenedInitialOrganizationId] =
    useState<string>();
  useEffect(() => {
    if (
      !initialOrganizationId ||
      openedInitialOrganizationId === initialOrganizationId ||
      tab !== "organizations"
    )
      return;
    setOpenedInitialOrganizationId(initialOrganizationId);
    orgMembers(initialOrganizationId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialOrganizationId, openedInitialOrganizationId, tab]);
  const loadContact = async (id: string) => {
    try {
      setContactDetail(await api<Contact>(`/api/admin/contact-requests/${id}`));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.actionError);
    }
  };
  const invite = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    await call("/api/admin/subscription-invitations", {
      method: "POST",
      body: JSON.stringify({
        userId: data.get("userId"),
        organizationId: data.get("organizationId"),
      }),
    });
    form.reset();
  };
  const renewSubscription = (subscription: Subscription) =>
    call(
      "/api/admin/subscription-invitations",
      {
        method: "POST",
        body: JSON.stringify({
          userId: subscription.userId,
          organizationId: subscription.organizationId,
        }),
      },
      language === "en"
        ? "A new subscription invitation was created."
        : language === "sq"
          ? "U krijua një ftesë e re për abonim."
          : "Креирана е нова покана за претплата.",
    );
  const activate = async (event: FormEvent<HTMLFormElement>, id: string) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    await call(`/api/admin/subscriptions/${id}/activate`, {
      method: "POST",
      body: JSON.stringify({
        paymentReference: data.get("paymentReference"),
        paymentNote: data.get("paymentNote"),
      }),
    });
  };
  const createTicketForClient = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    try {
      await api("/api/admin/tickets", {
        method: "POST",
        body: JSON.stringify({
          userId: data.get("userId"),
          organizationId: data.get("organizationId"),
          category: data.get("category"),
          priority: data.get("priority"),
          title: data.get("title"),
          description: data.get("description"),
        }),
      });
      form.reset();
      setSelectedClientId("");
      setCreatingTicket(false);
      setScopedError(null);
      refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.actionError);
    }
  };
  const role = async (event: FormEvent<HTMLFormElement>, id: string) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    await call(`/api/admin/users/${id}/roles`, {
      method: "POST",
      body: JSON.stringify({ roles: [data.get("role")] }),
    });
  };
  const removeRole = (id: string, roleName: string) =>
    call(`/api/admin/users/${id}/roles/${encodeURIComponent(roleName)}`, {
      method: "DELETE",
    });
  const assignContact = async (
    event: FormEvent<HTMLFormElement>,
    id: string,
  ) => {
    event.preventDefault();
    const userId = new FormData(event.currentTarget).get("userId");
    await call(
      `/api/admin/contact-requests/${id}/assign?userId=${encodeURIComponent(String(userId))}`,
    );
  };
  const linkContact = async (event: FormEvent<HTMLFormElement>, id: string) => {
    event.preventDefault();
    const organizationId = new FormData(event.currentTarget).get(
      "organizationId",
    );
    await call(
      `/api/admin/contact-requests/${id}/link-organization?organizationId=${encodeURIComponent(String(organizationId))}`,
    );
  };
  const saveContent = async (
    event: FormEvent<HTMLFormElement>,
    kind: "services" | "pages",
  ) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const title = String(data.get("contentTitle") ?? "").trim();
    const description = String(data.get("contentDescription") ?? "").trim();
    const sharedTranslation = { title, description };
    const successMessage =
      language === "en"
        ? "Content saved for all three languages."
        : language === "sq"
          ? "Përmbajtja u ruajt për të tri gjuhët."
          : "Содржината е зачувана на сите три јазици.";
    return call(
      `/api/admin/${kind}`,
      {
        method: "POST",
        body: JSON.stringify({
          slug: String(data.get("slug") ?? "").trim(),
          status: data.get("status"),
          category: String(data.get("category") ?? "General").trim() || "General",
          translations: {
            mk: sharedTranslation,
            en: sharedTranslation,
            sq: sharedTranslation,
          },
        }),
      },
      successMessage,
    );
  };
  const saveSetting = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    await call(`/api/admin/settings/${data.get("key")}`, {
      method: "PUT",
      body: JSON.stringify({
        value: data.get("value"),
        description: data.get("description"),
      }),
    });
  };
  const savePaymentInstructions = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const fields = [
      "PAYMENT_RECIPIENT",
      "PAYMENT_BANK",
      "PAYMENT_ACCOUNT",
      "PAYMENT_IBAN",
      "PAYMENT_SWIFT",
      "PAYMENT_AMOUNT",
      "PAYMENT_CURRENCY",
      "PAYMENT_PURPOSE",
      "PAYMENT_REFERENCE_INSTRUCTION",
      "PAYMENT_SUPPORT_EMAIL",
    ];
    try {
      await Promise.all(
        fields.map((key) =>
          api(`/api/admin/settings/${key}`, {
            method: "PUT",
            body: JSON.stringify({
              value: String(data.get(key) ?? "").trim(),
              description: "Offline subscription payment instruction",
            }),
          }),
        ),
      );
      setScopedError(null);
      refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.actionError);
    }
  };
  const exportReport = async (
    dataset = "tickets",
    format: "xlsx" | "csv" = "xlsx",
  ) => {
    const query = format === "csv" ? "?format=csv" : "";
    const response = await fetch(
      `/api/admin/reports/export/${dataset}${query}`,
      {
        headers: { Authorization: `Bearer ${getAccessToken() ?? ""}` },
      },
    );
    if (!response.ok) return setError(t.reportDownloadError);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = `digitmak-${dataset}.${format}`;
    link.click();
    URL.revokeObjectURL(url);
  };
  const uploadEvidence = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const file = data.get("file");
    if (!(file instanceof File)) return setError(t.chooseEvidenceFile);
    const relatedEntityId = String(data.get("relatedEntityId") ?? "").trim();
    if (
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
        relatedEntityId,
      )
    ) {
      return setError(
        language === "mk"
          ? "Поврзаниот ID мора да биде валиден системски UUID, а не реден број."
          : language === "sq"
            ? "ID-ja e lidhur duhet të jetë UUID valide e sistemit, jo numër rendor."
            : "The related ID must be a valid system UUID, not a sequence number.",
      );
    }
    const upload = new FormData();
    upload.append("file", file);
    const query = new URLSearchParams({
      relatedEntityType: String(data.get("relatedEntityType")),
      relatedEntityId,
    });
    for (const key of [
      "kpiCategory",
      "reportingPeriod",
      "templateType",
    ] as const) {
      const value = String(data.get(key) ?? "").trim();
      if (value) query.set(key, value);
    }
    const templateId = String(data.get("templateId") ?? "").trim();
    if (templateId) query.set("templateId", templateId);
    await call(`/api/admin/evidence?${query}`, {
      method: "POST",
      body: upload,
    });
    form.reset();
    setEvidenceRelatedId("");
    setShowEvidenceTargets(false);
    setEvidenceView("register");
  };
  const downloadEvidence = async (fileId: string) => {
    const response = await fetch(`/api/files/${fileId}`, {
      headers: { Authorization: `Bearer ${getAccessToken() ?? ""}` },
    });
    if (!response.ok) return setError(t.evidenceDownloadError);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download =
      response.headers
        .get("content-disposition")
        ?.match(/filename="?([^";]+)"?/)?.[1] ?? "digitmak-evidence";
    link.click();
    URL.revokeObjectURL(url);
  };
  const downloadAttachment = async (attachment: AdminAttachment) => {
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
  const downloadTemplate = async (
    template: EvidenceTemplate,
    format: "xlsx" | "csv",
  ) => {
    const query = format === "csv" ? "?format=csv" : "";
    const response = await fetch(
      `/api/admin/evidence-templates/${template.id}/blank${query}`,
      { headers: { Authorization: `Bearer ${getAccessToken() ?? ""}` } },
    );
    if (!response.ok) return setError(t.reportDownloadError);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = `${template.code}-template.${format}`;
    link.click();
    URL.revokeObjectURL(url);
  };
  const settingValue = (key: string) =>
    settings.data?.find((item) => item.key === key)?.value ?? "";
  const eligibleClients = (users.data ?? []).filter(
    (item) =>
      item.roles.includes("Client") &&
      !item.roles.includes("Admin") &&
      item.status === "Active" &&
      !!item.organizationId,
  );
  const selectedClient = eligibleClients.find(
    (item) => item.id === selectedClientId,
  );
  const selectedOrganizations = (organizations.data ?? []).filter(
    (organization) =>
      organization.id === selectedClient?.organizationId &&
      organization.status === "Approved",
  );
  const selectedSubscriptionUser = (users.data ?? []).find(
    (item) => item.id === selectedSubscriptionUserId,
  );
  const visibleTickets = (tickets.data ?? []).filter(
    (item) =>
      (!ticketSearch ||
        `${item.ticketNumber} ${item.title}`
          .toLocaleLowerCase()
          .includes(ticketSearch.toLocaleLowerCase())) &&
      (!ticketStatus || item.status === ticketStatus) &&
      (!ticketCategory || item.category === ticketCategory),
  );
  if (user && !allowed)
    return (
      <section className="page">
        <h1>{t.noAdminAccess}</h1>
        <button className="secondary" onClick={() => onNavigate("dashboard")}>
          {t.back}
        </button>
      </section>
    );
  const menu: { key: Tab; label: string }[] = [
    { key: "overview", label: t.overview },
    {
      key: "myNotifications",
      label:
        language === "en"
          ? "Notifications"
          : language === "sq"
            ? "Njoftimet"
            : "Известувања",
    },
    { key: "tickets", label: t.tickets },
    { key: "organizations", label: t.organizations },
    {
      key: "changes",
      label:
        language === "mk"
          ? "Барања за промена"
          : language === "sq"
            ? "Kërkesa për ndryshim"
            : "Change requests",
    },
    { key: "subscriptions", label: t.subscriptions },
    { key: "contacts", label: t.contacts },
    { key: "documents", label: t.documents },
    { key: "users", label: t.users },
    { key: "content", label: t.content },
    { key: "reports", label: t.reports },
    {
      key: "evidence",
      label:
        language === "en"
          ? "KPI documentation"
          : language === "sq"
            ? "Dokumentacioni KPI"
            : "KPI документација",
    },
    { key: "settings", label: t.settings },
    {
      key: "notifications",
      label:
        language === "en"
          ? "Email delivery"
          : language === "sq"
            ? "Dërgimi i email-eve"
            : "Испраќање е-пораки",
    },
    { key: "audit", label: t.audit },
  ];
  return (
    <section className="dashboard admin">
      <aside>
        <div className="user">
          <span>DA</span>
          <div>
            <b>{user?.email ?? "DigitMak Admin"}</b>
            <small>{t.administrator}</small>
          </div>
        </div>
        {menu.map((item) =>
          item.key === "myNotifications" ? (
            <div className="notifications-anchor" key={item.key}>
              <button
                className={notificationsPopupOpen ? "sel" : ""}
                onClick={() => setNotificationsPopupOpen((value) => !value)}
              >
                <span>
                  {item.label}
                  {unreadMyNotifications > 0 && (
                    <span className="menu-badge">{unreadMyNotifications}</span>
                  )}
                </span>
                <span>›</span>
              </button>
              {notificationsPopupOpen && (
                <NotificationsPopup
                  items={myNotifications.data ?? []}
                  language={language}
                  onClose={() => setNotificationsPopupOpen(false)}
                  onOpenItem={openMyNotification}
                  onMarkAllRead={markAllMyNotificationsRead}
                  onDelete={deleteMyNotification}
                />
              )}
            </div>
          ) : (
            <button
              className={tab === item.key ? "sel" : ""}
              key={item.key}
              onClick={() => {
                setTab(item.key);
                setSelectedTicket(null);
                setScopedError(null);
              }}
            >
              <span>
                {item.label}
                {item.key === "tickets" &&
                  (kpis.data?.newTickets ?? 0) > 0 && (
                    <span className="menu-badge">{kpis.data?.newTickets}</span>
                  )}
              </span>
              <span>›</span>
            </button>
          ),
        )}
        <button onClick={() => onNavigate("staff")}>
          {t.workspace} <span>›</span>
        </button>
        <button className="logout" onClick={() => onNavigate("dashboard")}>
          {t.clientPortal}
        </button>
      </aside>
      <div className="dash-main">
        <div className="dash-head">
          <div>
            <span>{t.adminCenter}</span>
            <h1>{menu.find((item) => item.key === tab)?.label}</h1>
          </div>
        </div>
        {((scopedError?.tab === tab && scopedError.message) ||
          (tab === "overview" && kpis.error)) && (
          <p className="form-error dashboard-feedback">
            {scopedError?.tab === tab ? scopedError.message : kpis.error}
          </p>
        )}
        {scopedSuccess?.tab === tab && (
          <p className="form-success dashboard-feedback">{scopedSuccess.message}</p>
        )}
        {tab === "overview" && (
          <div className="stats">
            <article>
              <span>{t.activeSubscriptions}</span>
              <b>{kpis.data?.activeSubscriptions ?? 0}</b>
              <small>
                {kpis.data?.expiredSubscriptions ?? 0} {t.expiredCancelled}
              </small>
            </article>
            <article>
              <span>{t.tickets}</span>
              <b>{kpis.data?.tickets ?? 0}</b>
            </article>
            <article>
              <span>{t.contactRequests}</span>
              <b>{kpis.data?.contactRequests ?? 0}</b>
              <small>
                {kpis.data?.publicInstitutions ?? 0} {t.publicInstitutions}
              </small>
            </article>
          </div>
        )}
        {tab === "organizations" && (
          <>
            <div className="ticket-list">
              <div className="list-head">
                <h2>{t.organizations}</h2>
              </div>
              {(organizations.data ?? []).map((item) => (
                <div className="approval" key={item.id}>
                  <div>
                    <b>{item.name}</b>
                    <small>
                      {labelFor(item.type, language)} · {item.region ?? "—"} ·{" "}
                      {labelFor(item.status, language)}
                    </small>
                  </div>
                  <button onClick={() => orgMembers(item.id)}>
                    {t.members}
                  </button>
                  {item.status === "PendingApproval" && (
                    <>
                      <button
                        className="approve"
                        onClick={() => orgAction(item.id, "approve")}
                      >
                        {t.approve}
                      </button>
                      <button
                        className="reject"
                        onClick={() => orgAction(item.id, "reject")}
                      >
                        {t.reject}
                      </button>
                    </>
                  )}
                  {item.status === "Approved" && (
                    <button
                      className="reject"
                      onClick={() => orgAction(item.id, "suspend")}
                    >
                      {t.suspend}
                    </button>
                  )}
                  {item.status === "Suspended" && (
                    <button
                      className="approve"
                      onClick={() => orgAction(item.id, "reactivate")}
                    >
                      {t.reactivate}
                    </button>
                  )}
                </div>
              ))}
            </div>
            {orgDetail && (
              <div className="workspace">
                <h2>
                  {t.members}: {orgDetail.organization.name}
                </h2>
                {orgDetail.members.map((member) => (
                  <div className="approval" key={member.id}>
                    <div>
                      <b>
                        {member.firstName} {member.lastName}
                      </b>
                      <small>
                        {member.email} · {member.memberStatus}
                      </small>
                    </div>
                    {member.memberStatus === "Pending" && (
                      <>
                        <button
                          className="approve"
                          onClick={() =>
                            call(
                              `/api/admin/organization-members/${member.id}/approve`,
                            )
                          }
                        >
                          {t.approve}
                        </button>
                        <button
                          className="reject"
                          onClick={() =>
                            call(
                              `/api/admin/organization-members/${member.id}/reject`,
                            )
                          }
                        >
                          {t.reject}
                        </button>
                      </>
                    )}
                  </div>
                ))}
              </div>
            )}
          </>
        )}
        {(tab === "subscriptions" || tab === "changes") && (
          <div
            className={`workspace two-column ${tab === "changes" ? "change-requests-workspace" : "subscriptions-workspace"}`}
          >
            <div>
              <section className="account-change-admin">
                <h2>
                  {language === "mk"
                    ? "Барања за промена"
                    : language === "sq"
                      ? "Kërkesa për ndryshim"
                      : "Change requests"}
                </h2>
                {(accountChanges.data ?? []).map((item) => (
                  <article className="account-change-item" key={item.id}>
                    <div>
                      <span
                        className={`tag ${item.status === "Accepted" ? "status-complete" : item.status === "Declined" ? "status-new" : "status-progress"}`}
                      >
                        {item.status === "Pending"
                          ? language === "mk"
                            ? "Се разгледува"
                            : "Under review"
                          : item.status === "Accepted"
                            ? language === "mk"
                              ? "Одобрено"
                              : "Approved"
                            : language === "mk"
                              ? "Одбиено"
                              : "Declined"}
                      </span>
                      <h3>
                        {item.firstName} {item.lastName}
                      </h3>
                      <small>
                        {item.email} · {labelFor(item.requestType, language)}
                      </small>
                      <p>{item.details}</p>
                      {item.decisionNote && <p>{item.decisionNote}</p>}
                    </div>
                    {item.status === "Pending" && (
                      <form
                        className="account-change-decision"
                        onSubmit={async (event) => {
                          event.preventDefault();
                          const form = event.currentTarget;
                          const data = new FormData(form);
                          const submitter = (event.nativeEvent as SubmitEvent)
                            .submitter as HTMLButtonElement | null;
                          try {
                            await api(
                              `/api/admin/account-change-requests/${item.id}/decision`,
                              {
                                method: "POST",
                                body: JSON.stringify({
                                  status: submitter?.value,
                                  note: data.get("note"),
                                }),
                              },
                            );
                            setScopedSuccess({
                              tab: "changes",
                              message:
                                language === "mk"
                                  ? "Одлуката е зачувана и клиентот е известен."
                                  : "The decision was saved and the client was notified.",
                            });
                            refresh();
                          } catch (reason) {
                            setError(
                              reason instanceof Error
                                ? reason.message
                                : language === "mk"
                                  ? "Одлуката не може да се зачува."
                                  : "The decision could not be saved.",
                            );
                          }
                        }}
                      >
                        <input
                          name="note"
                          placeholder={
                            language === "mk"
                              ? "Објаснување за клиентот"
                              : "Note for the client"
                          }
                        />
                        <div className="action-row">
                          <button
                            className="approve"
                            name="decision"
                            value="Accepted"
                          >
                            {t.approve}
                          </button>
                          <button
                            className="reject"
                            name="decision"
                            value="Declined"
                          >
                            {t.reject}
                          </button>
                        </div>
                      </form>
                    )}
                  </article>
                ))}
                {!accountChanges.loading &&
                  !(accountChanges.data ?? []).length && (
                    <p className="empty-state">
                      {language === "mk"
                        ? "Нема барања за промена."
                        : "There are no change requests."}
                    </p>
                  )}
              </section>
              <form className="workspace-form" onSubmit={invite}>
                <h2>{t.newInvitation}</h2>
                <label>
                  {t.user}
                  <select
                    name="userId"
                    required
                    value={selectedSubscriptionUserId}
                    onChange={(event) =>
                      setSelectedSubscriptionUserId(event.target.value)
                    }
                  >
                    <option value="">{t.choose}</option>
                    {(users.data ?? [])
                      .filter(
                        (item) =>
                          item.organizationId &&
                          item.status === "Active" &&
                          item.roles.includes("Client") &&
                          !item.roles.includes("Admin") &&
                          (organizations.data ?? []).some(
                            (organization) =>
                              organization.id === item.organizationId &&
                              organization.status === "Approved",
                          ) &&
                          !(subscriptions.data ?? []).some(
                            (subscription) =>
                              subscription.userId === item.id &&
                              subscription.status === "Active",
                          ),
                      )
                      .map((item) => (
                        <option value={item.id} key={item.id}>
                          {item.email}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  {t.organization}
                  <select
                    name="organizationId"
                    required
                    disabled={!selectedSubscriptionUser}
                    value={selectedSubscriptionUser?.organizationId ?? ""}
                  >
                    <option value="">{t.choose}</option>
                    {(organizations.data ?? [])
                      .filter(
                        (item) =>
                          item.status === "Approved" &&
                          item.id === selectedSubscriptionUser?.organizationId,
                      )
                      .map((item) => (
                        <option value={item.id} key={item.id}>
                          {item.name}
                        </option>
                      ))}
                  </select>
                </label>
                <button className="primary">{t.sendInvitation}</button>
                {(subscriptions.data ?? []).some(
                  (subscription) =>
                    subscription.status === "Active" &&
                    users.data?.find((item) => item.id === subscription.userId)
                      ?.email === "client@digitmak.mk",
                ) && (
                  <p>
                    {language === "en"
                      ? "Demo client: active subscription — no invitation is pending. See its status in the list on the right."
                      : language === "sq"
                        ? "Klienti demo: abonim aktiv — nuk ka ftesë në pritje. Statusi shfaqet në listën djathtas."
                        : "Demo клиент: активна претплата — нема покана на чекање. Статусот е прикажан во листата десно."}
                  </p>
                )}
              </form>
              {!settings.loading && (
                <form
                  className="workspace-form payment-settings-form"
                  onSubmit={savePaymentInstructions}
                >
                  <h2>{t.paymentSettings}</h2>
                  <p>{t.paymentSettingsHelp}</p>
                  <label>
                    {t.paymentRecipient}
                    <input
                      name="PAYMENT_RECIPIENT"
                      defaultValue={settingValue("PAYMENT_RECIPIENT")}
                    />
                  </label>
                  <label>
                    {t.paymentBank}
                    <input
                      name="PAYMENT_BANK"
                      defaultValue={settingValue("PAYMENT_BANK")}
                    />
                  </label>
                  <label>
                    {t.paymentAccount}
                    <input
                      name="PAYMENT_ACCOUNT"
                      defaultValue={settingValue("PAYMENT_ACCOUNT")}
                    />
                  </label>
                  <div className="row">
                    <label>
                      IBAN
                      <input
                        name="PAYMENT_IBAN"
                        defaultValue={settingValue("PAYMENT_IBAN")}
                      />
                    </label>
                    <label>
                      SWIFT
                      <input
                        name="PAYMENT_SWIFT"
                        defaultValue={settingValue("PAYMENT_SWIFT")}
                      />
                    </label>
                  </div>
                  <div className="row">
                    <label>
                      {t.paymentAmount}
                      <input
                        name="PAYMENT_AMOUNT"
                        inputMode="decimal"
                        defaultValue={settingValue("PAYMENT_AMOUNT")}
                      />
                    </label>
                    <label>
                      {t.paymentCurrency}
                      <input
                        name="PAYMENT_CURRENCY"
                        placeholder="MKD / EUR"
                        defaultValue={settingValue("PAYMENT_CURRENCY")}
                      />
                    </label>
                  </div>
                  <label>
                    {t.paymentPurpose}
                    <input
                      name="PAYMENT_PURPOSE"
                      defaultValue={settingValue("PAYMENT_PURPOSE")}
                    />
                  </label>
                  <label>
                    {t.paymentReferenceGuide}
                    <textarea
                      name="PAYMENT_REFERENCE_INSTRUCTION"
                      rows={3}
                      defaultValue={settingValue(
                        "PAYMENT_REFERENCE_INSTRUCTION",
                      )}
                    />
                  </label>
                  <label>
                    {t.paymentSupportEmail}
                    <input
                      name="PAYMENT_SUPPORT_EMAIL"
                      type="email"
                      defaultValue={settingValue("PAYMENT_SUPPORT_EMAIL")}
                    />
                  </label>
                  <button className="primary">{t.savePaymentSettings}</button>
                </form>
              )}
            </div>
            <div>
              <h2>{t.subscriptions}</h2>
              {(subscriptions.data ?? []).some(
                (item) => item.status === "PendingPayment",
              ) && (
                <p className="notice padded">{t.pendingPaymentInstruction}</p>
              )}
              {(subscriptions.data ?? []).map((item) => (
                <article className="meeting-card" key={item.id}>
                  <span className="tag blue">
                    {labelFor(item.status, language)}
                  </span>
                  <p>
                    {users.data?.find((x) => x.id === item.userId)?.email ??
                      item.userId}
                  </p>
                  {item.status === "PendingPayment" && (
                    <form
                      className="workspace-form"
                      onSubmit={(event) => activate(event, item.id)}
                    >
                      <h3>{t.confirmOfflinePayment}</h3>
                      <input
                        name="paymentReference"
                        required
                        placeholder={t.reference}
                      />
                      <input name="paymentNote" placeholder={t.note} />
                      <button className="approve">{t.activate}</button>
                    </form>
                  )}
                  {item.status === "Active" && (
                    <button
                      className="reject"
                      onClick={() =>
                        call(`/api/admin/subscriptions/${item.id}/cancel`)
                      }
                    >
                      {t.cancel}
                    </button>
                  )}
                  {["Cancelled", "Expired"].includes(item.status) && (
                    <button
                      className="approve"
                      onClick={() => renewSubscription(item)}
                    >
                      {t.renewSubscription}
                    </button>
                  )}
                  {item.expiresAt && (
                    <small>
                      {t.validUntil}{" "}
                      {new Date(item.expiresAt).toLocaleDateString(
                        localeFor(language),
                      )}
                    </small>
                  )}
                </article>
              ))}
              <h2>
                {language === "en"
                  ? "Invitation history"
                  : language === "sq"
                    ? "Historiku i ftesave"
                    : "Историја на покани"}
              </h2>
              {(invitations.data ?? []).length === 0 && (
                <p>
                  {language === "en"
                    ? "No subscription invitations."
                    : language === "sq"
                      ? "Nuk ka ftesa për abonim."
                      : "Нема покани за претплата."}
                </p>
              )}
              {(invitations.data ?? []).map((item) => (
                <article className="meeting-card" key={item.id}>
                  <span className="tag blue">
                    {labelFor(item.status, language)}
                  </span>
                  <p>
                    {users.data?.find((userItem) => userItem.id === item.userId)
                      ?.email ?? item.userId}
                  </p>
                  <small>
                    {language === "en"
                      ? "Expires"
                      : language === "sq"
                        ? "Skadon"
                        : "Истекува"}
                    :{" "}
                    {new Date(item.expiresAt).toLocaleDateString(
                      localeFor(language),
                    )}
                  </small>
                </article>
              ))}
            </div>
          </div>
        )}
        {tab === "contacts" && (
          <div className="ticket-list">
            <div className="list-head">
              <h2>DMA {t.contactRequests}</h2>
            </div>
            {(contacts.data ?? []).map((item) => (
              <div className="approval contact-request-card" key={item.id}>
                <button
                  type="button"
                  className="contact-request-summary"
                  onClick={() => void loadContact(item.id)}
                >
                  <b>{item.organizationName}</b>
                  <small>
                    {item.contactName} · {item.email} ·{" "}
                    {labelFor(item.dmaCategory, language)} ·{" "}
                    {labelFor(item.status, language)}
                  </small>
                  <span className="contact-request-open-label">
                    {language === "mk"
                      ? "Отвори"
                      : language === "sq"
                        ? "Hap"
                        : "Open"}
                  </span>
                </button>
                <div className="contact-request-actions" hidden>
                  <button
                    className="secondary"
                    onClick={() => void loadContact(item.id)}
                  >
                    {t.details}
                  </button>
                  {item.status !== "Handled" && (
                    <button
                      className="approve"
                      onClick={() =>
                        call(
                          `/api/admin/contact-requests/${item.id}/mark-handled`,
                        )
                      }
                    >
                      {t.handled}
                    </button>
                  )}
                  <button
                    onClick={() =>
                      call(
                        `/api/admin/contact-requests/${item.id}/invite-registration`,
                        { method: "POST" },
                        language === "en"
                          ? "The registration invitation was added to the email queue."
                          : language === "sq"
                            ? "Ftesa e regjistrimit u shtua në radhën e email-eve."
                            : "Поканата за регистрација е додадена во редот за испраќање е-пошта.",
                      )
                    }
                  >
                    {t.inviteRegistration}
                  </button>
                  <form
                    className="inline-form"
                    onSubmit={(event) => assignContact(event, item.id)}
                  >
                    <select
                      name="userId"
                      required
                      defaultValue={item.assignedTo ?? ""}
                    >
                      <option value="">
                        {language === "en"
                          ? "Assign staff"
                          : language === "sq"
                            ? "Cakto stafin"
                            : "Додели на staff"}
                      </option>
                      {(staffUsers.data ?? []).map((staffUser) => (
                        <option
                          key={`${staffUser.id}-${staffUser.role}`}
                          value={staffUser.id}
                        >
                          {staffUser.email} ·{" "}
                          {labelFor(staffUser.role, language)}
                        </option>
                      ))}
                    </select>
                    <button>{t.assign}</button>
                  </form>
                  <form
                    className="inline-form"
                    onSubmit={(event) => linkContact(event, item.id)}
                  >
                    <select
                      name="organizationId"
                      required
                      defaultValue={item.linkedOrganizationId ?? ""}
                    >
                      <option value="">
                        {language === "en"
                          ? "Link organisation"
                          : language === "sq"
                            ? "Lidh organizatën"
                            : "Поврзи организација"}
                      </option>
                      {(organizations.data ?? [])
                        .filter(
                          (organization) => organization.status === "Approved",
                        )
                        .map((organization) => (
                          <option key={organization.id} value={organization.id}>
                            {organization.name}
                          </option>
                        ))}
                    </select>
                    <button>
                      {language === "en"
                        ? "Link"
                        : language === "sq"
                          ? "Lidh"
                          : "Поврзи"}
                    </button>
                  </form>
                  <form
                    className="inline-form"
                    onSubmit={(event) => {
                      event.preventDefault();
                      const data = new FormData(event.currentTarget);
                      void call(
                        `/api/admin/contact-requests/${item.id}/respond`,
                        {
                          method: "POST",
                          body: JSON.stringify({ body: data.get("body") }),
                        },
                      );
                    }}
                  >
                    <input name="body" required placeholder={t.emailReply} />
                    <button className="approve">{t.send}</button>
                  </form>
                </div>
              </div>
            ))}
            {!contacts.loading && !(contacts.data ?? []).length && (
              <div className="empty-state">
                <h3>{t.noContactRequests}</h3>
                <p>{t.noContactRequestsHelp}</p>
              </div>
            )}
            {contactDetail && (
              <div
                className="contact-request-overlay"
                role="presentation"
                onMouseDown={(event) => {
                  if (event.target === event.currentTarget) {
                    setContactDetail(null);
                  }
                }}
              >
                <section
                  className="workspace organization-detail contact-request-detail"
                  role="dialog"
                  aria-modal="true"
                  aria-label={contactDetail.organizationName}
                  onMouseDown={(event) => event.stopPropagation()}
                >
                  <button
                    className="back-link"
                    onClick={() => setContactDetail(null)}
                  >
                    × {t.close}
                  </button>
                  <span className="kicker">{t.dmaContactRequest}</span>
                  <h2>{contactDetail.organizationName}</h2>
                  <section className="contact-detail-actions">
                    <div className="contact-action-group">
                      <span className="contact-action-label">
                        {language === "mk"
                          ? "Статус на барањето"
                          : language === "sq"
                            ? "Statusi i kërkesës"
                            : "Request status"}
                      </span>
                      <div className="contact-action-buttons">
                        {contactDetail.status !== "Handled" && (
                          <button
                            className="approve"
                            onClick={() =>
                              call(
                                `/api/admin/contact-requests/${contactDetail.id}/mark-handled`,
                              )
                            }
                          >
                            {t.handled}
                          </button>
                        )}
                        <button
                          className="secondary"
                          onClick={() =>
                            call(
                              `/api/admin/contact-requests/${contactDetail.id}/invite-registration`,
                              { method: "POST" },
                              language === "en"
                                ? "The registration invitation was added to the email queue."
                                : language === "sq"
                                  ? "Ftesa e regjistrimit u shtua në radhën e email-eve."
                                  : "Поканата за регистрација е додадена во редот за испраќање е-пошта.",
                            )
                          }
                        >
                          {t.inviteRegistration}
                        </button>
                      </div>
                    </div>
                    <div className="contact-action-group">
                      <span className="contact-action-label">
                        {language === "mk"
                          ? "Додели одговорен"
                          : language === "sq"
                            ? "Cakto përgjegjësin"
                            : "Assign owner"}
                      </span>
                      <form
                        className="inline-form"
                        onSubmit={(event) =>
                          assignContact(event, contactDetail.id)
                        }
                      >
                        <select
                          name="userId"
                          required
                          defaultValue={contactDetail.assignedTo ?? ""}
                        >
                          <option value="">{t.assign}</option>
                          {(staffUsers.data ?? []).map((staffUser) => (
                            <option key={staffUser.id} value={staffUser.id}>
                              {staffUser.email} ·{" "}
                              {labelFor(staffUser.role, language)}
                            </option>
                          ))}
                        </select>
                        <button className="primary">{t.assign}</button>
                      </form>
                    </div>
                    <div className="contact-action-group">
                      <span className="contact-action-label">
                        {language === "mk"
                          ? "Поврзи со организација"
                          : language === "sq"
                            ? "Lidh me organizatën"
                            : "Link to organisation"}
                      </span>
                      <form
                        className="inline-form"
                        onSubmit={(event) => linkContact(event, contactDetail.id)}
                      >
                        <select
                          name="organizationId"
                          required
                          defaultValue={contactDetail.linkedOrganizationId ?? ""}
                        >
                          <option value="">{t.organization}</option>
                          {(organizations.data ?? [])
                            .filter((item) => item.status === "Approved")
                            .map((item) => (
                              <option key={item.id} value={item.id}>
                                {item.name}
                              </option>
                            ))}
                        </select>
                        <button className="primary">
                          {language === "mk" ? "Поврзи" : "Link"}
                        </button>
                      </form>
                    </div>
                    <div className="contact-action-group contact-action-wide">
                      <span className="contact-action-label">
                        {language === "mk"
                          ? "Одговори по е-пошта до подносителот"
                          : language === "sq"
                            ? "Përgjigju me email dërguesit"
                            : "Reply by email to the submitter"}
                      </span>
                      <form
                        className="inline-form contact-reply-form"
                        onSubmit={(event) => {
                          event.preventDefault();
                          const data = new FormData(event.currentTarget);
                          void call(
                            `/api/admin/contact-requests/${contactDetail.id}/respond`,
                            {
                              method: "POST",
                              body: JSON.stringify({ body: data.get("body") }),
                            },
                          );
                        }}
                      >
                        <input name="body" required placeholder={t.emailReply} />
                        <button className="approve">{t.send}</button>
                      </form>
                    </div>
                  </section>
                  <dl className="detail-grid">
                    <DetailRow
                      label={dma.type}
                      value={labelFor(contactDetail.organizationType, language)}
                    />
                    <DetailRow
                      label={dma.sector}
                      value={contactDetail.sector}
                    />
                    <DetailRow
                      label={dma.municipality}
                      value={contactDetail.municipality}
                    />
                    <DetailRow
                      label={dma.region}
                      value={contactDetail.region}
                    />
                    <DetailRow
                      label={dma.website}
                      value={contactDetail.website}
                    />
                    <DetailRow
                      label={dma.employees}
                      value={contactDetail.employeeCount}
                    />
                    <DetailRow
                      label={dma.fullName}
                      value={contactDetail.contactName}
                    />
                    <DetailRow label={dma.email} value={contactDetail.email} />
                    <DetailRow label={dma.phone} value={contactDetail.phone} />
                    <DetailRow
                      label={dma.language}
                      value={contactDetail.preferredLanguage.toUpperCase()}
                    />
                    <DetailRow
                      label={dma.selfRating}
                      value={contactDetail.digitalMaturityRating}
                    />
                    <DetailRow
                      label={t.dmaCategory}
                      value={labelFor(contactDetail.dmaCategory, language)}
                    />
                    <DetailRow
                      label={dma.mainNeed}
                      value={labelFor(contactDetail.mainNeed, language)}
                    />
                    <DetailRow
                      label={dma.challenge}
                      value={contactDetail.challengeDescription}
                    />
                    <DetailRow
                      label={dma.tools}
                      value={contactDetail.currentTools}
                    />
                    <DetailRow
                      label={dma.dataSources}
                      value={contactDetail.currentDataSources}
                    />
                    <DetailRow
                      label={dma.usesAi}
                      value={
                        contactDetail.usesAi == null
                          ? "—"
                          : contactDetail.usesAi
                            ? dma.yes
                            : dma.no
                      }
                    />
                    <DetailRow
                      label={dma.useCase}
                      value={contactDetail.aiUseCase}
                    />
                    <DetailRow
                      label={dma.privacyConcerns}
                      value={contactDetail.privacyConcerns}
                    />
                    <DetailRow
                      label={dma.aiAct}
                      value={
                        contactDetail.interestedInAiActGuidance
                          ? dma.yes
                          : dma.no
                      }
                    />
                    <DetailRow
                      label={dma.trainingNeeds}
                      value={contactDetail.trainingNeeds}
                    />
                    <DetailRow
                      label={dma.timeline}
                      value={contactDetail.desiredTimeline}
                    />
                    <DetailRow
                      label={dma.format}
                      value={contactDetail.preferredConsultationFormat}
                    />
                    <DetailRow
                      label={dma.consent}
                      value={contactDetail.consentToContact ? dma.yes : dma.no}
                    />
                    <DetailRow
                      label={dma.privacy}
                      value={
                        contactDetail.privacyPolicyAccepted ? dma.yes : dma.no
                      }
                    />
                    <DetailRow
                      label={t.status}
                      value={labelFor(contactDetail.status, language)}
                    />
                    <DetailRow
                      label={t.created}
                      value={new Date(contactDetail.createdAt).toLocaleString(
                        localeFor(language),
                      )}
                    />
                  </dl>
                </section>
              </div>
            )}
          </div>
        )}
        {tab === "tickets" &&
          (selectedTicket ? (
            <StaffTicketDetail
              ticket={selectedTicket}
              staff={staffUsers.data ?? []}
              onBack={() => setSelectedTicket(null)}
              onChanged={() => {
                refresh();
                setSelectedTicket(null);
              }}
              onOrganization={() => {
                setSelectedTicket(null);
                setTab("organizations");
              }}
              language={language}
              canAssign
              onManageExperts={() => {
                setSelectedTicket(null);
                setTab("users");
              }}
            />
          ) : (
            <div className="ticket-list">
              <div className="list-head">
                <div>
                  <div className="ticket-list-heading">
                    <h2>
                      {language === "mk"
                        ? "Клиентски тикети"
                        : language === "sq"
                          ? "Tiketat e klientëve"
                          : "Client tickets"}
                    </h2>
                    <span className="ticket-count">
                      {tickets.data?.length ?? 0}{" "}
                      {language === "mk"
                        ? "тикети"
                        : language === "sq"
                          ? "tiketa"
                          : "tickets"}
                    </span>
                  </div>
                  <p>
                    {language === "mk"
                      ? "Преглед, доделување и одговарање на сите клиентски барања."
                      : language === "sq"
                        ? "Shikoni, caktoni dhe përgjigjuni të gjitha kërkesave të klientëve."
                        : "Review, assign and reply to all client requests."}
                  </p>
                </div>
                <button
                  className="ticket-create-toggle"
                  type="button"
                  onClick={() => setCreatingTicket((value) => !value)}
                >
                  +{" "}
                  {language === "mk"
                    ? "Тикет во име на клиент"
                    : language === "sq"
                      ? "Tiket në emër të klientit"
                      : "Ticket on behalf of client"}
                </button>
              </div>
              {creatingTicket && (
                <form
                  className="workspace-form admin-ticket-create"
                  onSubmit={createTicketForClient}
                >
                  <h3>
                    {language === "mk"
                      ? "Креирај тикет во име на клиент"
                      : language === "sq"
                        ? "Krijo tiket në emër të klientit"
                        : "Create a ticket on behalf of a client"}
                  </h3>
                  <div className="row">
                    <label>
                      {language === "mk"
                        ? "Клиент"
                        : language === "sq"
                          ? "Klienti"
                          : "Client"}
                      <select
                        name="userId"
                        required
                        value={selectedClientId}
                        onChange={(event) =>
                          setSelectedClientId(event.target.value)
                        }
                      >
                        <option value="">{t.choose}</option>
                        {eligibleClients.map((item) => (
                          <option key={item.id} value={item.id}>
                            {item.firstName} {item.lastName} · {item.email}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      {t.organization}
                      <select
                        name="organizationId"
                        required
                        disabled={!selectedClientId}
                      >
                        <option value="">{t.choose}</option>
                        {selectedOrganizations.map((item) => (
                          <option key={item.id} value={item.id}>
                            {item.name}
                          </option>
                        ))}
                      </select>
                    </label>
                  </div>
                  <div className="row">
                    <label>
                      {language === "mk"
                        ? "Категорија"
                        : language === "sq"
                          ? "Kategoria"
                          : "Category"}
                      <select name="category" required>
                        {[
                          "AI_READINESS",
                          "AI_ACT_COMPLIANCE",
                          "AI_USE_CASE",
                          "DATA_GOVERNANCE",
                          "AUTOMATION_AND_INTELLIGENCE",
                          "TEST_BEFORE_INVEST",
                          "DIGITALIZATION_ROADMAP",
                          "TRAINING_AND_SKILLS",
                          "FUNDING_AND_INVESTMENT",
                          "REFERRAL",
                          "OTHER",
                        ].map((value) => (
                          <option key={value} value={value}>
                            {labelFor(value, language)}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      {language === "mk"
                        ? "Приоритет"
                        : language === "sq"
                          ? "Prioriteti"
                          : "Priority"}
                      <select name="priority" defaultValue="Normal">
                        {["Low", "Normal", "High", "Urgent"].map((value) => (
                          <option key={value} value={value}>
                            {labelFor(value, language)}
                          </option>
                        ))}
                      </select>
                    </label>
                  </div>
                  <label>
                    {language === "mk"
                      ? "Наслов"
                      : language === "sq"
                        ? "Titulli"
                        : "Title"}
                    <input name="title" required />
                  </label>
                  <label>
                    {language === "mk"
                      ? "Опис"
                      : language === "sq"
                        ? "Përshkrimi"
                        : "Description"}
                    <textarea name="description" rows={5} required />
                  </label>
                  <button className="primary">
                    {language === "mk"
                      ? "Креирај за клиентот"
                      : language === "sq"
                        ? "Krijo për klientin"
                        : "Create for client"}
                  </button>
                </form>
              )}
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
                  {["New", "Assigned", "InProgress", "Resolved", "Closed"].map(
                    (value) => (
                      <option key={value} value={value}>
                        {labelFor(value, language)}
                      </option>
                    ),
                  )}
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
                    new Set((tickets.data ?? []).map((item) => item.category)),
                  ).map((value) => (
                    <option key={value} value={value}>
                      {labelFor(value, language)}
                    </option>
                  ))}
                </select>
              </div>
              {visibleTickets.map((item) => (
                <button
                  className="ticket ticket-button"
                  key={item.id}
                  onClick={() => setSelectedTicket(item)}
                >
                  <span className={`tag ${ticketStatusClass(item.status)}`}>
                    {labelFor(item.status, language)}
                  </span>
                  <div>
                    <b>
                      {item.ticketNumber} — {item.title}
                    </b>
                    <small>
                      {labelFor(item.category, language)} ·{" "}
                      {labelFor(item.priority, language)}
                    </small>
                  </div>
                  <span>
                    {new Date(item.createdAt).toLocaleDateString(
                      language === "mk" ? "mk-MK" : language,
                    )}
                  </span>
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
          ))}
        {tab === "documents" && (
          <div className="ticket-list document-list">
            <div className="list-head">
              <div>
                <h2>{t.allTicketDocuments}</h2>
                <p>{t.allTicketDocumentsHelp}</p>
              </div>
              <span className="tag blue">{attachments.data?.length ?? 0}</span>
            </div>
            {(attachments.data ?? []).map((attachment) => (
              <div className="approval" key={attachment.id}>
                <span className="tag blue">
                  {attachment.contentType === "application/pdf"
                    ? "PDF"
                    : t.file}
                </span>
                <div>
                  <b>{attachment.originalFilename}</b>
                  <small>
                    #{attachment.ticketNumber} · {attachment.ticketTitle} ·{" "}
                    {attachment.organizationName}
                  </small>
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
            {!attachments.loading && !(attachments.data ?? []).length && (
              <div className="empty-state">
                <h3>{t.noTicketDocuments}</h3>
                <p>{t.noTicketDocumentsHelp}</p>
              </div>
            )}
          </div>
        )}
        {tab === "users" && (
          <div className="ticket-list">
            <div className="list-head">
              <h2>{t.usersRoles}</h2>
            </div>
            {(users.data ?? []).map((item) => (
              <div className="approval" key={item.id}>
                <div>
                  <b>
                    {item.firstName} {item.lastName}
                  </b>
                  <small>
                    {item.email} · {labelFor(item.status, language)} ·{" "}
                    {t.created}:{" "}
                    {new Date(item.createdAt).toLocaleDateString(
                      localeFor(language),
                    )}
                    {item.emailVerifiedAt
                      ? ` · ${t.emailVerified}: ${new Date(item.emailVerifiedAt).toLocaleDateString(localeFor(language))}`
                      : ""}
                  </small>
                  <div className="action-row">
                    {item.roles.map((roleName) => (
                      <button
                        key={roleName}
                        type="button"
                        className="secondary"
                        disabled={item.id === user?.id && roleName === "Admin"}
                        onClick={() => void removeRole(item.id, roleName)}
                      >
                        {labelFor(roleName, language)} ×
                      </button>
                    ))}
                  </div>
                </div>
                <form
                  className="inline-form"
                  onSubmit={(event) => role(event, item.id)}
                >
                  <select name="role">
                    {["Client", "HelpDeskAgent", "Expert", "Admin"].map(
                      (roleName) => (
                        <option key={roleName} value={roleName}>
                          {labelFor(roleName, language)}
                        </option>
                      ),
                    )}
                  </select>
                  <button className="approve">{t.addRole}</button>
                </form>
                <button
                  className="reject"
                  onClick={() =>
                    call(`/api/admin/users/${item.id}`, {
                      method: "PATCH",
                      body: JSON.stringify({
                        status:
                          item.status === "Active" ? "Inactive" : "Active",
                        preferredLanguage: item.preferredLanguage,
                        phone: item.phoneNumber,
                      }),
                    })
                  }
                >
                  {item.status === "Active" ? t.deactivate : t.activate}
                </button>
              </div>
            ))}
          </div>
        )}
        {tab === "content" && (
          <div className="workspace evidence-workspace content-workspace">
            <ContentForm
              title={t.service}
              onSubmit={(event) => saveContent(event, "services")}
              language={language}
              category
            />
            <ContentForm
              title={t.publicPage}
              onSubmit={(event) => saveContent(event, "pages")}
              language={language}
            />
            <ContentInventory
              title={
                language === "mk"
                  ? "Зачувани услуги"
                  : language === "sq"
                    ? "Shërbimet e ruajtura"
                    : "Saved services"
              }
              collection={serviceContent.data}
              loading={serviceContent.loading}
              error={serviceContent.error}
              language={language}
            />
            <ContentInventory
              title={
                language === "mk"
                  ? "Зачувани јавни страници"
                  : language === "sq"
                    ? "Faqet publike të ruajtura"
                    : "Saved public pages"
              }
              collection={pageContent.data}
              loading={pageContent.loading}
              error={pageContent.error}
              language={language}
            />
          </div>
        )}
        {tab === "reports" && (
          <>
            <div className="stats">
              <article>
                <span>{t.aiHelpSubscriptions}</span>
                <b>{kpis.data?.aiHelpDeskSubscriptions ?? 0}</b>
              </article>
              <article>
                <span>{t.confirmedMeetings}</span>
                <b>{kpis.data?.confirmedMeetings ?? 0}</b>
              </article>
              <article>
                <span>{t.referrals}</span>
                <b>{kpis.data?.referrals ?? 0}</b>
              </article>
            </div>
            <div className="workspace">
              <h2>{t.detailedReports}</h2>
              <div className="calendar-grid">
                <ReportSection
                  title={t.contactsBreakdown}
                  groups={
                    contactReport.data
                      ? {
                          organizationType:
                            contactReport.data.byOrganizationType,
                          sector: contactReport.data.bySector,
                          region: contactReport.data.byRegion,
                          need: contactReport.data.byNeed,
                          dmaCategory: contactReport.data.byDmaCategory,
                        }
                      : {}
                  }
                  language={language}
                />
                <ReportSection
                  title={t.ticketsBreakdown}
                  groups={
                    ticketReport.data
                      ? {
                          category: ticketReport.data.byCategory,
                          status: ticketReport.data.byStatus,
                          assignee: ticketReport.data.byAssignee.map(
                            (group) => ({
                              ...group,
                              key:
                                users.data?.find(
                                  (user) => user.id === group.key,
                                )?.email ?? group.key,
                            }),
                          ),
                          organizationType:
                            ticketReport.data.byOrganizationType,
                        }
                      : {}
                  }
                  language={language}
                />
                <ReportSection
                  title={t.meetingsBreakdown}
                  groups={
                    meetingReport.data
                      ? {
                          status: meetingReport.data.byStatus,
                          type: meetingReport.data.byType,
                        }
                      : {}
                  }
                  language={language}
                />
                <ReportSection
                  title={t.referralsBreakdown}
                  groups={{ recommendation: referralReport.data ?? [] }}
                  language={language}
                />
              </div>
            </div>
            <div className="kpi-card report-export-card">
              <h2>{t.exportData}</h2>
              <div className="report-export-grid">
                {["tickets", "contacts", "meetings", "subscriptions"].map(
                  (dataset) => (
                    <section className="report-export-item" key={dataset}>
                      <b>{labelFor(dataset, language)}</b>
                      <div className="action-row">
                        <button
                          onClick={() => void exportReport(dataset, "xlsx")}
                        >
                          {t.downloadExcel}
                        </button>
                        <button
                          onClick={() => void exportReport(dataset, "csv")}
                        >
                          {t.downloadCsv}
                        </button>
                      </div>
                    </section>
                  ),
                )}
              </div>
            </div>
          </>
        )}
        {tab === "evidence" && (
          <div className="workspace evidence-workspace evidence-center">
            <header className="evidence-center-header">
              <div>
                <span className="kicker">DIGITMAK KPI</span>
                <h2>
                  {language === "mk"
                    ? "KPI документација"
                    : language === "sq"
                      ? "Dokumentacioni KPI"
                      : "KPI documentation"}
                </h2>
                <p>
                  {language === "mk"
                    ? "Креирајте документ, преземете образец или прегледајте го регистарот — без долга страница со сите алатки одеднаш."
                    : language === "sq"
                      ? "Krijoni dokument, shkarkoni model ose shikoni regjistrin në një vend."
                      : "Create a document, download a template or review the register in one place."}
                </p>
              </div>
              <div className="evidence-summary" aria-label="KPI summary">
                <b>{evidence.data?.length ?? 0}</b>
                <span>
                  {language === "mk"
                    ? "документи"
                    : language === "sq"
                      ? "dokumente"
                      : "documents"}
                </span>
              </div>
            </header>
            <nav
              className="evidence-view-tabs"
              aria-label="KPI documentation views"
            >
              {(
                [
                  [
                    "upload",
                    language === "mk"
                      ? "Нов документ"
                      : language === "sq"
                        ? "Dokument i ri"
                        : "New document",
                  ],
                  [
                    "templates",
                    language === "mk"
                      ? "Обрасци"
                      : language === "sq"
                        ? "Modele"
                        : "Templates",
                  ],
                  [
                    "register",
                    language === "mk"
                      ? "Регистар"
                      : language === "sq"
                        ? "Regjistri"
                        : "Register",
                  ],
                ] as const
              ).map(([view, label]) => (
                <button
                  key={view}
                  type="button"
                  className={evidenceView === view ? "active" : ""}
                  onClick={() => setEvidenceView(view)}
                >
                  {label}
                  {view === "templates" && (
                    <small>{evidenceTemplates.data?.length ?? 0}</small>
                  )}
                  {view === "register" && (
                    <small>{evidence.data?.length ?? 0}</small>
                  )}
                </button>
              ))}
            </nav>
            <form
              className="workspace-form evidence-upload-form"
              onSubmit={uploadEvidence}
              hidden={evidenceView !== "upload"}
            >
              <h2>
                {language === "en"
                  ? "New KPI document"
                  : language === "sq"
                    ? "Dokument i ri KPI"
                    : "Нов KPI документ"}
              </h2>
              <label>
                {t.relatedType}
                <select
                  name="relatedEntityType"
                  required
                  value={evidenceEntityType}
                  onChange={(event) => {
                    setEvidenceEntityType(event.target.value);
                    setEvidenceRelatedId("");
                    setShowEvidenceTargets(false);
                    setScopedError(null);
                  }}
                >
                  {[
                    "Ticket",
                    "Meeting",
                    "Subscription",
                    "ContactRequest",
                    "KpiPeriod",
                  ].map((type) => (
                    <option key={type} value={type}>
                      {labelFor(type, language)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                {t.relatedId}
                <div className="evidence-id-control">
                  <input
                    name="relatedEntityId"
                    required
                    pattern="[0-9a-fA-F-]{36}"
                    placeholder="UUID"
                    value={evidenceRelatedId}
                    onChange={(event) =>
                      setEvidenceRelatedId(event.target.value)
                    }
                  />
                  <button
                    type="button"
                    className="secondary"
                    onClick={() => setShowEvidenceTargets((value) => !value)}
                  >
                    {language === "mk"
                      ? "Види UUID"
                      : language === "sq"
                        ? "Shih UUID"
                        : "View UUID"}
                  </button>
                </div>
                {showEvidenceTargets && (
                  <div className="evidence-target-picker">
                    <small>
                      {language === "mk"
                        ? "Изберете запис — UUID ќе се внесе автоматски."
                        : language === "sq"
                          ? "Zgjidhni regjistrin — UUID plotësohet automatikisht."
                          : "Select a record — its UUID will be filled automatically."}
                    </small>
                    {evidenceTargets.loading && <p>{t.loading}</p>}
                    {(evidenceTargets.data ?? []).map((target) => (
                      <button
                        type="button"
                        key={target.id}
                        onClick={() => {
                          setEvidenceRelatedId(target.id);
                          setShowEvidenceTargets(false);
                        }}
                      >
                        <b>{target.label}</b>
                        <code>{target.id}</code>
                      </button>
                    ))}
                    {!evidenceTargets.loading &&
                      !(evidenceTargets.data ?? []).length && (
                        <p>
                          {language === "mk"
                            ? "Нема достапни записи."
                            : language === "sq"
                              ? "Nuk ka regjistra."
                              : "No records available."}
                        </p>
                      )}
                  </div>
                )}
              </label>
              <label>
                {t.kpiCategory}
                <input name="kpiCategory" />
              </label>
              <label>
                {t.period}
                <input name="reportingPeriod" placeholder="2026-Q3" />
              </label>
              <label>
                {t.template}
                <select name="templateId">
                  <option value="">{t.selectTemplate}</option>
                  {(evidenceTemplates.data ?? [])
                    .filter(
                      (item) =>
                        item.isActive &&
                        item.relatedEntityType === evidenceEntityType,
                    )
                    .map((item) => (
                      <option key={item.id} value={item.id}>
                        {evidenceTemplateView(item, language).title}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                {t.file}
                <input
                  name="file"
                  type="file"
                  accept=".pdf,.png,.jpg,.jpeg,.txt,.docx,.csv,.xlsx"
                  required
                />
              </label>
              <button className="primary">
                {language === "en"
                  ? "Upload document"
                  : language === "sq"
                    ? "Ngarko dokumentin"
                    : "Прикачи документ"}{" "}
              </button>
            </form>
            <div
              className="evidence-template-panel"
              hidden={evidenceView !== "templates"}
            >
              <div className="evidence-section-heading">
                <span className="kicker">DIGITMAK KPI</span>
                <h2>
                  {language === "en"
                    ? "KPI document templates"
                    : language === "sq"
                      ? "Modelet e dokumenteve KPI"
                      : "Обрасци за KPI документација"}
                </h2>
                <p>
                  {language === "mk"
                    ? "Изберете готов Excel образец, пополнете ги означените полиња и прикачете го како KPI документација."
                    : language === "sq"
                      ? "Zgjidhni një model Excel, plotësoni fushat dhe ngarkojeni si dokumentacion KPI."
                      : "Choose an Excel template, complete the marked fields and upload it as KPI documentation."}
                </p>
              </div>
              <div className="evidence-template-list">
                {(evidenceTemplates.data ?? []).map((item) => {
                  const presentation = evidenceTemplateView(item, language);
                  const fieldCount = (() => {
                    try {
                      return (JSON.parse(item.requiredMetadataJson) as string[])
                        .length;
                    } catch {
                      return 0;
                    }
                  })();
                  return (
                    <article className="evidence-template-card" key={item.id}>
                      <div
                        className="evidence-template-icon"
                        aria-hidden="true"
                      >
                        XLSX
                      </div>
                      <div className="evidence-template-copy">
                        <span>
                          {labelFor(item.relatedEntityType, language)}
                        </span>
                        <h3>{presentation.title}</h3>
                        <p>{presentation.description}</p>
                        <small>
                          {fieldCount}{" "}
                          {language === "mk"
                            ? "полиња за пополнување"
                            : language === "sq"
                              ? "fusha për plotësim"
                              : "fields to complete"}
                        </small>
                      </div>
                      <div className="template-download-actions">
                        <button
                          type="button"
                          className="format-button format-button-primary"
                          onClick={() => void downloadTemplate(item, "xlsx")}
                        >
                          Excel
                        </button>
                        <button
                          type="button"
                          className="format-button"
                          onClick={() => void downloadTemplate(item, "csv")}
                        >
                          CSV
                        </button>
                      </div>
                    </article>
                  );
                })}
              </div>
            </div>
            <div
              className="evidence-register-panel"
              hidden={evidenceView !== "register"}
            >
              <h2 className="evidence-register-title">
                {language === "en"
                  ? "KPI document register"
                  : language === "sq"
                    ? "Regjistri i dokumenteve KPI"
                    : "Регистар на KPI документација"}
              </h2>
              {(evidence.data ?? []).map((item) => (
                <article className="meeting-card" key={item.id}>
                  <span className="tag blue">{item.relatedEntityType}</span>
                  <b>{item.kpiCategory ?? item.templateType ?? "Evidence"}</b>
                  <p>{item.relatedEntityId}</p>
                  <small>
                    {item.reportingPeriod ?? "—"} ·{" "}
                    {new Date(item.createdAt).toLocaleDateString()}
                  </small>
                  <button
                    className="secondary"
                    onClick={() => downloadEvidence(item.fileId)}
                  >
                    {t.download}
                  </button>
                </article>
              ))}
              {!evidence.loading && !(evidence.data ?? []).length && (
                <p className="empty-state">
                  {language === "mk"
                    ? "Сè уште нема прикачени KPI документи."
                    : language === "sq"
                      ? "Ende nuk ka dokumente KPI të ngarkuara."
                      : "No KPI documents have been uploaded yet."}
                </p>
              )}
            </div>
          </div>
        )}
        {tab === "settings" && (
          <div className="workspace two-column">
            <form className="workspace-form" onSubmit={saveSetting}>
              <h2>{t.systemSetting}</h2>
              <label>
                {t.key}
                <input name="key" defaultValue="DataRetentionDays" required />
              </label>
              <label>
                {t.value}
                <input name="value" required />
              </label>
              <label>
                {t.description}
                <input name="description" />
              </label>
              <button className="primary">{t.save}</button>
            </form>
            <div>
              <h2>{t.settings}</h2>
              {(settings.data ?? []).map((item) => (
                <article className="meeting-card" key={item.id}>
                  <b>{item.key}</b>
                  <p>{item.value}</p>
                  <small>{item.description}</small>
                </article>
              ))}
            </div>
          </div>
        )}
        {tab === "notifications" && (
          <div className="ticket-list">
            <div className="list-head">
              <h2>
                {language === "en"
                  ? "Email delivery queue"
                  : language === "sq"
                    ? "Radha e dërgimit të email-eve"
                    : "Ред за испраќање е-пораки"}
              </h2>
              <span>{notifications.data?.length ?? 0}</span>
            </div>
            {(notifications.data ?? []).map((item) => (
              <div className="approval" key={item.id}>
                <span
                  className={`tag ${item.status === "Failed" ? "amber" : "blue"}`}
                >
                  {labelFor(item.status, language)}
                </span>
                <div>
                  <b>{item.subject}</b>
                  <small>
                    {item.recipientEmail ??
                      users.data?.find(
                        (userItem) => userItem.id === item.recipientUserId,
                      )?.email ??
                      item.recipientUserId ??
                      "—"}{" "}
                    · {item.type} ·{" "}
                    {new Date(item.createdAt).toLocaleString(
                      localeFor(language),
                    )}
                  </small>
                  {item.lastError && <small>{item.lastError}</small>}
                </div>
                {item.status === "Failed" && (
                  <button
                    className="approve"
                    disabled={retryingNotificationId === item.id}
                    onClick={() => void retryNotification(item.id)}
                  >
                    {retryingNotificationId === item.id
                      ? language === "en"
                        ? "Queuing..."
                        : language === "sq"
                          ? "Duke vendosur në radhë..."
                          : "Се закажува..."
                      : language === "en"
                        ? "Retry"
                        : language === "sq"
                          ? "Provo përsëri"
                          : "Обиди се повторно"}
                  </button>
                )}
              </div>
            ))}
          </div>
        )}
        {tab === "audit" && (
          <div className="ticket-list">
            <div className="list-head">
              <h2>{t.systemActivities}</h2>
            </div>
            {(audits.data ?? []).map((item) => (
              <div className="ticket" key={item.id}>
                <span className="tag blue">{item.entityType}</span>
                <div>
                  <b>{item.action}</b>
                  <small>{item.entityId}</small>
                </div>
                <span>
                  {new Date(item.createdAt).toLocaleString(localeFor(language))}
                </span>
              </div>
            ))}
          </div>
        )}
        {preview && (
          <DocumentPreview
            document={preview}
            onClose={() => setPreview(undefined)}
          />
        )}
      </div>
    </section>
  );
}

function ContentForm({
  title,
  onSubmit,
  language,
  category = false,
}: {
  title: string;
  onSubmit: (event: FormEvent<HTMLFormElement>) => Promise<boolean>;
  language: Language;
  category?: boolean;
}) {
  const [contentTitle, setContentTitle] = useState("");
  const [contentDescription, setContentDescription] = useState("");
  const [validationError, setValidationError] = useState("");
  const editorCopy = {
    mk: {
      slug: "URL ознака",
      status: "Статус",
      category: "Категорија",
      content: "Содржина",
      title: "Наслов",
      description: "Опис",
      save: "Зачувај",
      required: "Внесете наслов и опис.",
      shared: "Истата содржина автоматски ќе се прикаже на македонски, англиски и албански.",
    },
    en: {
      slug: "URL slug",
      status: "Status",
      category: "Category",
      content: "Content",
      title: "Title",
      description: "Description",
      save: "Save",
      required: "Enter a title and description.",
      shared: "The same content will automatically be shown in Macedonian, English and Albanian.",
    },
    sq: {
      slug: "Shenja e URL-së",
      status: "Statusi",
      category: "Kategoria",
      content: "Përmbajtja",
      title: "Titulli",
      description: "Përshkrimi",
      save: "Ruaj",
      required: "Vendosni titullin dhe përshkrimin.",
      shared: "E njëjta përmbajtje do të shfaqet automatikisht në maqedonisht, anglisht dhe shqip.",
    },
  }[language];

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    if (!contentTitle.trim() || !contentDescription.trim()) {
      setValidationError(editorCopy.required);
      return;
    }
    setValidationError("");
    const saved = await onSubmit(event);
    if (!saved) return;
    form.reset();
    setContentTitle("");
    setContentDescription("");
  };

  return (
    <form className="workspace-form" onSubmit={submit}>
      <h2>{title}</h2>
      <label>
        {editorCopy.slug}
        <input
          name="slug"
          required
          pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
          placeholder="digital-roadmap"
        />
      </label>
      <label>
        {editorCopy.status}
        <select name="status" defaultValue="Published">
          {["Published", "Draft", "Archived"].map((status) => (
            <option key={status} value={status}>
              {labelFor(status, language)}
            </option>
          ))}
        </select>
      </label>
      {category && (
        <label>
          {editorCopy.category}
          <input name="category" defaultValue="General" required />
        </label>
      )}
      <fieldset className="content-localization-editor">
        <legend>{editorCopy.content}</legend>
        <p className="muted">{editorCopy.shared}</p>
        <label>
          {editorCopy.title}
          <input
            name="contentTitle"
            value={contentTitle}
            onChange={(event) => {
              setContentTitle(event.target.value);
              setValidationError("");
            }}
          />
        </label>
        <label>
          {editorCopy.description}
          <textarea
            name="contentDescription"
            rows={3}
            value={contentDescription}
            onChange={(event) => {
              setContentDescription(event.target.value);
              setValidationError("");
            }}
          />
        </label>
      </fieldset>
      {validationError && <p className="form-error">{validationError}</p>}
      <button className="primary">{editorCopy.save}</button>
    </form>
  );
}

function ContentInventory({
  title,
  collection,
  loading,
  error,
  language,
}: {
  title: string;
  collection: ContentCollection | null;
  loading: boolean;
  error: string;
  language: Language;
}) {
  const translation = (
    entityId: string,
    locale: "mk" | "en" | "sq",
    fieldName: string,
  ) =>
    collection?.translations.find(
      (item) =>
        item.entityId === entityId &&
        item.language === locale &&
        item.fieldName === fieldName,
    )?.value ?? "";
  const emptyLabel =
    language === "mk"
      ? "Нема зачувана содржина."
      : language === "sq"
        ? "Nuk ka përmbajtje të ruajtur."
        : "No saved content.";
  return (
    <section className="content-inventory">
      <div className="list-head">
        <h2>{title}</h2>
        <small>
          {collection?.items.length ?? 0}{" "}
          {language === "mk"
            ? "ставки"
            : language === "sq"
              ? "artikuj"
              : "items"}
        </small>
      </div>
      {loading && <p>{language === "mk" ? "Се вчитува…" : "Loading…"}</p>}
      {error && <p className="form-error">{error}</p>}
      {!loading && !collection?.items.length && <p>{emptyLabel}</p>}
      <div className="content-inventory-grid">
        {(collection?.items ?? []).map((item) => (
          <article className="content-inventory-card" key={item.id}>
            <div className="content-inventory-head">
              <div>
                <b>{translation(item.id, language, "title") || item.slug}</b>
                <small>/{item.slug}</small>
              </div>
              <span className={`tag ${item.status === "Published" ? "green" : "blue"}`}>
                {labelFor(item.status, language)}
              </span>
            </div>
            {item.category && <small>{item.category}</small>}
            <p>
              {translation(item.id, language, "description") ||
                (language === "mk"
                  ? "Нема опис на избраниот јазик."
                  : "No description in the selected language.")}
            </p>
            <details>
              <summary>
                {language === "mk"
                  ? "Прикажи ги сите преводи"
                  : language === "sq"
                    ? "Shfaq të gjitha përkthimet"
                    : "Show all translations"}
              </summary>
              {(["mk", "en", "sq"] as const).map((locale) => (
                <div className="content-translation-preview" key={locale}>
                  <b>{locale.toUpperCase()}</b>
                  <span>{translation(item.id, locale, "title") || "—"}</span>
                  <p>{translation(item.id, locale, "description") || "—"}</p>
                </div>
              ))}
            </details>
          </article>
        ))}
      </div>
    </section>
  );
}

function ReportSection({
  title,
  groups,
  language,
}: {
  title: string;
  groups: Record<string, CountGroup[]>;
  language: Language;
}) {
  const t = workspaceCopy(language);
  return (
    <article className="meeting-card">
      <h3>{title}</h3>
      {Object.entries(groups).map(([name, rows]) => (
        <div key={name}>
          <b>
            {t.groupBy} {labelFor(name, language)}
          </b>
          {rows.length === 0 ? (
            <p>—</p>
          ) : (
            rows.map((row) => (
              <p key={`${name}-${row.key}`}>
                {labelFor(row.key, language)}: <strong>{row.count}</strong>
              </p>
            ))
          )}
        </div>
      ))}
    </article>
  );
}

function DetailRow({
  label,
  value,
}: {
  label: string;
  value?: string | number | null;
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>
        {value === undefined || value === null || value === "" ? "—" : value}
      </dd>
    </div>
  );
}
