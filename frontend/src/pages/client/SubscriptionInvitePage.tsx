import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api } from "../../api";
import { useAuth } from "../../features/auth/useAuth";
import type { Navigate } from "../../shared/types";
import { usePortalLanguage } from "../../shared/usePortalLanguage";
export function SubscriptionInvitePage({
  onNavigate,
}: {
  onNavigate: Navigate;
}) {
  const language = usePortalLanguage();
  const t =
    language === "en"
      ? {
          kicker: "SUBSCRIPTION",
          title: "Personal annual subscription.",
          lead: "The invitation is valid for 14 days. After acceptance and recorded offline payment, an administrator activates it for 12 months.",
          login: "Sign in with the account that received the invitation.",
          toLogin: "Go to sign in",
          accept: "Accept invitation",
          accepted:
            "Invitation accepted. The subscription is waiting for offline payment confirmation.",
          error: "The invitation could not be accepted.",
          portal: "Open portal",
        }
      : language === "sq"
        ? {
            kicker: "ABONIMI",
            title: "Abonim personal vjetor.",
            lead: "Ftesa vlen 14 ditë. Pas pranimit dhe pagesës offline, administratori e aktivizon për 12 muaj.",
            login: "Hyni me llogarinë që e ka marrë ftesën.",
            toLogin: "Shko te hyrja",
            accept: "Prano ftesën",
            accepted:
              "Ftesa u pranua. Abonimi pret konfirmimin e pagesës offline.",
            error: "Ftesa nuk mund të pranohej.",
            portal: "Hap portalin",
          }
        : {
            kicker: "ПРЕТПЛАТА",
            title: "Лична годишна претплата.",
            lead: "Поканата важи 14 дена. По прифаќањето и евидентираната офлајн уплата, администраторот ја активира претплатата за 12 месеци.",
            login: "Најавете се со корисникот за кој е испратена поканата.",
            toLogin: "Кон најава",
            accept: "Прифати покана",
            accepted:
              "Поканата е прифатена. Претплатата очекува потврда за офлајн уплата.",
            error: "Поканата не може да се прифати.",
            portal: "Отвори портал",
          };
  const [query] = useSearchParams();
  const { isAuthenticated } = useAuth();
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const token =
    query.get("token") ??
    sessionStorage.getItem("digitmak.subscriptionToken") ??
    "";
  useEffect(() => {
    if (token) sessionStorage.setItem("digitmak.subscriptionToken", token);
  }, [token]);
  const accept = async () => {
    try {
      await api(
        `/api/subscription-invitations/${encodeURIComponent(token)}/accept`,
        { method: "POST" },
      );
      sessionStorage.removeItem("digitmak.subscriptionToken");
      setMessage(t.accepted);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.error);
    }
  };
  return (
    <section className="page split">
      <div>
        <span className="kicker">{t.kicker}</span>
        <h1>{t.title}</h1>
        <p className="lead">{t.lead}</p>
      </div>
      <div className="login-card">
        {!isAuthenticated ? (
          <>
            <p>{t.login}</p>
            <button className="primary" onClick={() => onNavigate("help")}>
              {t.toLogin}
            </button>
          </>
        ) : (
          <button className="primary" disabled={!token} onClick={accept}>
            {t.accept}
          </button>
        )}
        {message && <p className="notice">{message}</p>}
        {error && <p className="form-error">{error}</p>}
        {message && (
          <button className="secondary" onClick={() => onNavigate("dashboard")}>
            {t.portal}
          </button>
        )}
      </div>
    </section>
  );
}
