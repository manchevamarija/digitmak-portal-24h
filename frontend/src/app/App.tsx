import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import "../App.css";
import { Footer } from "../components/layout/Footer";
import { Header } from "../components/layout/Header";
import { AdminDashboardPage, type Tab as AdminTab } from "../pages/admin/AdminDashboardPage";
import {
  ForgotPasswordPage,
  RegisterPage,
  ResetPasswordPage,
  VerifyEmailPage,
} from "../pages/auth/AuthPages";
import { ClientDashboardPage, type Tab as ClientTab } from "../pages/client/ClientDashboardPage";
import { OrganizationOnboardingPage } from "../pages/client/OrganizationOnboardingPage";
import { SubscriptionInvitePage } from "../pages/client/SubscriptionInvitePage";
import { PrivacyPage, TermsPage } from "../pages/public/CompliancePages";
import { HelpDeskPage, HomePage } from "../pages/public/PublicPages";
import { DmaContactPage } from "../pages/public/DmaContactPage";
import { TranslatedServicesPage } from "../pages/public/TranslatedServicesPage";
import { TrainingPage } from "../pages/public/TrainingPage";
import { StaffDashboardPage } from "../pages/staff/StaffDashboardPage";
import { useAuth } from "../features/auth/useAuth";
import type { Language, View } from "../shared/types";

function AuthCheckingPlaceholder() {
  return (
    <section className="page" style={{ minHeight: "60vh" }} aria-busy="true" />
  );
}

const routes: Record<View, string> = {
  home: "/",
  services: "/services",
  training: "/training",
  help: "/help-desk",
  contact: "/contact",
  register: "/register",
  forgot: "/forgot-password",
  reset: "/reset-password",
  verify: "/verify-email",
  organization: "/organization",
  "subscription-invite": "/subscription-invite",
  privacy: "/privacy",
  terms: "/terms",
  dashboard: "/portal",
  staff: "/staff",
  admin: "/admin",
};
const views = Object.fromEntries(
  Object.entries(routes).map(([view, path]) => [path, view]),
) as Record<string, View>;

export default function App() {
  const { isAuthenticated, user, isLoading } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [language, setLanguage] = useState<Language>(
    () =>
      (typeof window.localStorage?.getItem === "function"
        ? (window.localStorage.getItem("digitmak.language") as Language)
        : null) || "mk",
  );
  const legacy = new URLSearchParams(location.search).get(
    "view",
  ) as View | null;
  const view = views[location.pathname] ?? legacy ?? "home";
  useEffect(() => {
    if (!user) return;
    const hasStaffAccess = user.roles.some((role) =>
      ["HelpDeskAgent", "Expert", "Admin"].includes(role),
    );
    if (view === "staff" && !hasStaffAccess)
      navigate(routes.dashboard, { replace: true });
    if (view === "admin" && !user.roles.includes("Admin"))
      navigate(routes.dashboard, { replace: true });
    if (view === "contact" && user.roles.includes("Admin"))
      navigate("/admin?tab=contacts", { replace: true });
  }, [navigate, user, view]);
  const requestedClientTab = new URLSearchParams(location.search).get("tab");
  const clientInitialTab = (
    [
      "overview",
      "organization",
      "contacts",
      "tickets",
      "meetings",
      "notifications",
      "profile",
    ] as const
  ).includes(requestedClientTab as ClientTab)
    ? (requestedClientTab as ClientTab)
    : undefined;
  const go = (next: View, options?: { tab?: string; ticket?: string }) =>
    navigate({
      pathname: routes[next],
      search: options
        ? `?${new URLSearchParams(
            Object.fromEntries(
              Object.entries(options).filter((entry) => Boolean(entry[1])),
            ) as Record<string, string>,
          ).toString()}`
        : "",
    });
  return (
    <div className="app">
      <Header
        language={language}
        view={view}
        onLanguage={setLanguage}
        onNavigate={go}
      />
      <main>
        {view === "home" && <HomePage language={language} onNavigate={go} />}
        {view === "services" && <TranslatedServicesPage onNavigate={go} />}
        {view === "training" && <TrainingPage onNavigate={go} />}
        {view === "help" && <HelpDeskPage onNavigate={go} />}
        {view === "contact" && <DmaContactPage onNavigate={go} />}
        {view === "register" && <RegisterPage onNavigate={go} />}
        {view === "forgot" && <ForgotPasswordPage onNavigate={go} />}
        {view === "reset" && <ResetPasswordPage onNavigate={go} />}
        {view === "verify" && <VerifyEmailPage onNavigate={go} />}
        {view === "organization" && (
          <OrganizationOnboardingPage onNavigate={go} />
        )}
        {view === "subscription-invite" && (
          <SubscriptionInvitePage onNavigate={go} />
        )}
        {view === "privacy" && <PrivacyPage />}
        {view === "terms" && <TermsPage />}
        {view === "dashboard" &&
          (isLoading ? (
            <AuthCheckingPlaceholder />
          ) : isAuthenticated ? (
            <ClientDashboardPage
              onNavigate={go}
              initialTab={clientInitialTab}
              initialTicketId={
                new URLSearchParams(location.search).get("ticket") ?? undefined
              }
            />
          ) : (
            <HelpDeskPage onNavigate={go} />
          ))}
        {view === "staff" &&
          (isLoading ? (
            <AuthCheckingPlaceholder />
          ) : isAuthenticated &&
            user?.roles.some((role) =>
              ["HelpDeskAgent", "Expert", "Admin"].includes(role),
            ) ? (
            <StaffDashboardPage
              onNavigate={go}
              initialTicketId={
                new URLSearchParams(location.search).get("ticket") ?? undefined
              }
            />
          ) : isAuthenticated ? (
            <ClientDashboardPage
              onNavigate={go}
              initialTab={clientInitialTab}
              initialTicketId={
                new URLSearchParams(location.search).get("ticket") ?? undefined
              }
            />
          ) : (
            <HelpDeskPage onNavigate={go} />
          ))}
        {view === "admin" &&
          (isLoading ? (
            <AuthCheckingPlaceholder />
          ) : isAuthenticated && user?.roles.includes("Admin") ? (
            <AdminDashboardPage
              onNavigate={go}
              initialTab={
                (
                  [
                    "overview",
                    "myNotifications",
                    "organizations",
                    "changes",
                    "subscriptions",
                    "contacts",
                    "tickets",
                    "documents",
                    "users",
                    "content",
                    "reports",
                    "evidence",
                    "settings",
                    "notifications",
                    "audit",
                  ] as const
                ).includes(
                  new URLSearchParams(location.search).get("tab") as AdminTab,
                )
                  ? (new URLSearchParams(location.search).get(
                      "tab",
                    ) as AdminTab)
                  : undefined
              }
              initialTicketId={
                new URLSearchParams(location.search).get("ticket") ?? undefined
              }
              initialOrganizationId={
                new URLSearchParams(location.search).get("org") ?? undefined
              }
            />
          ) : isAuthenticated ? (
            <ClientDashboardPage
              onNavigate={go}
              initialTab={clientInitialTab}
              initialTicketId={
                new URLSearchParams(location.search).get("ticket") ?? undefined
              }
            />
          ) : (
            <HelpDeskPage onNavigate={go} />
          ))}
      </main>
      <Footer onNavigate={go} />
    </div>
  );
}
