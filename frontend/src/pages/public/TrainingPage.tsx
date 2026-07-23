import type { Navigate } from "../../shared/types";
import { uiCopy } from "../../content/uiCopy";
import { usePortalLanguage } from "../../shared/usePortalLanguage";
import { useAuth } from "../../features/auth/useAuth";
import { api } from "../../api";
export function TrainingPage({ onNavigate }: { onNavigate: Navigate }) {
  const language = usePortalLanguage();
  const t = uiCopy[language].training;
  const { user } = useAuth();
  const moodleLabel =
    language === "en"
      ? "Open Moodle learning"
      : language === "sq"
        ? "Hap mësimin në Moodle"
        : "Отвори Moodle обуки";
  const openMoodle = async () => {
    try {
      const launch = await api<{ url: string }>(
        "/api/integrations/moodle/launch",
      );
      window.open(launch.url, "_blank", "noopener,noreferrer");
    } catch {
      onNavigate("contact");
    }
  };
  return (
    <section className="page">
      <span className="kicker">{t.kicker}</span>
      <h1>{t.title}</h1>
      <p className="lead">{t.lead}</p>
      <div className="cards">
        <article>
          <span className="num">01</span>
          <h3>{t.aiTitle}</h3>
          <p>{t.aiText}</p>
        </article>
        <article>
          <span className="num">02</span>
          <h3>{t.dataTitle}</h3>
          <p>{t.dataText}</p>
        </article>
        <article>
          <span className="num">03</span>
          <h3>{t.actTitle}</h3>
          <p>{t.actText}</p>
        </article>
      </div>
      <button
        className="primary training-cta"
        onClick={() => onNavigate("contact")}
      >
        {t.action}
      </button>
      {user && (
        <button className="secondary" onClick={() => void openMoodle()}>
          {moodleLabel}
        </button>
      )}
    </section>
  );
}
