import { useState } from "react";
import type { FormEvent } from "react";
import { api } from "../../api";
import { uiCopy } from "../../content/uiCopy";
import { usePortalLanguage } from "../../shared/usePortalLanguage";
import { labelFor } from "../../shared/labels";
import { useAuth } from "../../features/auth/useAuth";
import type { Navigate } from "../../shared/types";

const dmaCategories = [
  "DIGITAL_BUSINESS_STRATEGY",
  "DIGITAL_READINESS",
  "HUMAN_CENTRIC_DIGITALISATION",
  "DATA_MANAGEMENT",
  "AUTOMATION_AND_INTELLIGENCE",
  "GREEN_DIGITALISATION",
] as const;

const dmaCategoryLabel = {
  mk: "Област на дигитална трансформација",
  en: "Digital transformation area",
  sq: "Fusha e transformimit digjital",
};

const dmaOptionalLabels = {
  mk: {
    useCase: "Идеја за примена на AI",
    privacy: "Грижи за приватност и усогласеност",
  },
  en: {
    useCase: "AI use-case idea",
    privacy: "Privacy and compliance concerns",
  },
  sq: {
    useCase: "Ide për përdorimin e AI",
    privacy: "Shqetësime për privatësi dhe pajtueshmëri",
  },
};

export function DmaContactPage({ onNavigate }: { onNavigate?: Navigate }) {
  const language = usePortalLanguage();
  const t = uiCopy[language].dma;
  const { user } = useAuth();
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setError("");
    const value = (name: string) => String(data.get(name) ?? "").trim() || null;
    try {
      await api("/api/public/contact-requests", {
        method: "POST",
        body: JSON.stringify({
          organizationName: value("organization"),
          organizationType: value("organizationType"),
          sector: value("sector"),
          municipality: value("municipality"),
          region: value("region"),
          website: value("website"),
          contactName: value("contactName"),
          email: value("email"),
          phone: value("phone"),
          preferredLanguage: value("preferredLanguage") ?? "mk",
          employeeCount: value("employeeCount")
            ? Number(value("employeeCount"))
            : null,
          digitalMaturityRating: Number(value("digitalMaturityRating")),
          dmaCategory: value("dmaCategory"),
          mainNeed: value("mainNeed"),
          challengeDescription: value("challenge"),
          currentTools: value("currentTools"),
          currentDataSources: value("currentDataSources"),
          usesAi: value("usesAi") === "yes",
          aiUseCase: value("aiUseCase"),
          privacyConcerns: value("privacyConcerns"),
          interestedInAiActGuidance:
            data.get("interestedInAiActGuidance") === "on",
          trainingNeeds: value("trainingNeeds"),
          desiredTimeline: value("desiredTimeline"),
          preferredConsultationFormat: value("preferredConsultationFormat"),
          consentToContact: data.get("consent") === "on",
          privacyPolicyAccepted: data.get("privacy") === "on",
        }),
      });
      setSent(true);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.error);
    }
  };
  if (sent)
    return (
      <section className="page">
        <div className="success">
          <b>✓</b>
          <h1>{t.received}</h1>
          <p>{t.confirmation}</p>
          <p>
            {language === "mk"
              ? "Ќе добивате е-пошта кога барањето е примено, кога ќе премине во обработка и кога ќе биде обработено или ќе добиете одговор."
              : language === "sq"
                ? "Do të merrni email kur kërkesa pranohet, kur kalon në përpunim dhe kur përpunohet ose merr përgjigje."
                : "You will receive email updates when the request is received, moves into processing, and is processed or receives a response."}
          </p>
          <div className="actions">
            <button className="secondary" onClick={() => setSent(false)}>
              {t.newRequest}
            </button>
          </div>
        </div>
      </section>
    );
  return (
    <section className="page contact dma">
      <div>
        <span className="kicker">{t.kicker}</span>
        <h1>{t.title}</h1>
        <p className="lead">{t.lead}</p>
        <div className="contact-meta">
          <p>{t.response}</p>
          <p>{t.support}</p>
          <p>{t.consultation}</p>
        </div>
      </div>
      <form onSubmit={submit}>
        {user && onNavigate && (
          <div className="info-banner">
            <p>
              {language === "mk"
                ? "За help-desk поддршка и разговор со тимот користете тикет. Контакт формата е за општо барање, интерес за услуга, обука или претплата."
                : language === "sq"
                  ? "Për mbështetje help-desk dhe bisedë me ekipin përdorni tiketë. Formulari i kontaktit është për kërkesa të përgjithshme, shërbime, trajnime ose abonim."
                  : "For help-desk support and conversation with the team, use a ticket. The contact form is for general enquiries, services, training, or subscriptions."}
            </p>
            <button type="button" className="secondary" onClick={() => onNavigate("dashboard", { tab: "tickets" })}>
              {language === "mk" ? "Отвори тикети" : language === "sq" ? "Hap tiketët" : "Open tickets"}
            </button>
          </div>
        )}
        <h2>{t.organization}</h2>
        <div className="row">
          <label>
            {t.name}
            <input name="organization" required />
          </label>
          <label>
            {t.type}
            <select name="organizationType" required>
              <option value="SME">{labelFor("SME", language)}</option>
              <option value="MidCap">{labelFor("MidCap", language)}</option>
              <option value="PublicInstitution">
                {labelFor("PublicInstitution", language)}
              </option>
              <option value="Partner">{labelFor("Partner", language)}</option>
              <option value="Other">{labelFor("Other", language)}</option>
            </select>
          </label>
        </div>
        <div className="row">
          <label>
            {t.sector}
            <input name="sector" />
          </label>
          <label>
            {t.municipality}
            <input name="municipality" />
          </label>
        </div>
        <div className="row">
          <label>
            {t.region}
            <input name="region" />
          </label>
          <label>
            {t.website}
            <input name="website" type="url" />
          </label>
        </div>
        <label>
          {t.employees}
          <input name="employeeCount" type="number" min="1" />
        </label>
        <h2>{t.contact}</h2>
        <div className="row">
          <label>
            {t.fullName}
            <input
              name="contactName"
              required
              defaultValue={user ? `${user.firstName} ${user.lastName}` : ""}
            />
          </label>
          <label>
            {t.email}
            <input
              name="email"
              required
              type="email"
              defaultValue={user?.email ?? ""}
              readOnly={Boolean(user)}
            />
          </label>
        </div>
        <div className="row">
          <label>
            {t.phone}
            <input name="phone" type="tel" />
          </label>
          <label>
            {t.language}
            <select name="preferredLanguage" defaultValue={language}>
              <option value="mk">Македонски</option>
              <option value="en">English</option>
              <option value="sq">Shqip</option>
            </select>
          </label>
        </div>
        <h2>{t.maturity}</h2>
        <label>
          {language === "en"
            ? "Digital maturity self-assessment (1–5)"
            : language === "sq"
              ? "Vetëvlerësimi i pjekurisë digjitale (1–5)"
              : "Самооценување на дигиталната зрелост (1–5)"}
          <select name="digitalMaturityRating" required>
            <option value="1">1 — {t.beginner}</option>
            <option value="2">2</option>
            <option value="3">3</option>
            <option value="4">4</option>
            <option value="5">5 — {t.advanced}</option>
          </select>
        </label>
        <label>
          {dmaCategoryLabel[language]}
          <select name="dmaCategory" defaultValue="DIGITAL_READINESS" required>
            {dmaCategories.map((category) => (
              <option key={category} value={category}>
                {labelFor(category, language)}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t.mainNeed}
          <select name="mainNeed">
            <option value="AI">{t.ai}</option>
            <option value="Digitalization">{t.digitalization}</option>
            <option value="Training">{t.training}</option>
            <option value="Funding">{t.funding}</option>
            <option value="TestBeforeInvest">
              {labelFor("TEST_BEFORE_INVEST", language)}
            </option>
            <option value="Other">{t.other}</option>
          </select>
        </label>
        <label>
          {t.challenge}
          <textarea name="challenge" rows={5} required />
        </label>
        <label>
          {t.tools}
          <textarea name="currentTools" rows={2} />
        </label>
        <label>
          {t.dataSources}
          <textarea name="currentDataSources" rows={2} />
        </label>
        <div className="row">
          <label>
            {t.usesAi}
            <select name="usesAi">
              <option value="no">{t.no}</option>
              <option value="yes">{t.yes}</option>
            </select>
          </label>
          <label>
            {t.timeline}
            <span className="date-input-wrap">
              <input type="date" name="desiredTimeline" min={new Date().toISOString().slice(0, 10)} aria-label={t.timeline} />
              <span aria-hidden="true">📅</span>
            </span>
          </label>
        </div>
        <label>
          {dmaOptionalLabels[language].useCase}
          <textarea name="aiUseCase" rows={3} />
        </label>
        <label>
          {dmaOptionalLabels[language].privacy}
          <textarea name="privacyConcerns" rows={3} />
        </label>
        <label>
          {t.trainingNeeds}
          <textarea name="trainingNeeds" rows={3} />
        </label>
        <label>
          {t.format}
          <select name="preferredConsultationFormat">
            <option value="NoPreference">{t.noPreference}</option>
            <option value="Online">{t.online}</option>
            <option value="Offline">{t.inPerson}</option>
          </select>
        </label>
        <label className="check">
          <input name="interestedInAiActGuidance" type="checkbox" /> {t.aiAct}
        </label>
        <label className="check">
          <input name="consent" required type="checkbox" /> {t.consent}
        </label>
        <label className="check">
          <input name="privacy" required type="checkbox" /> {t.privacy}
        </label>
        {error && <p className="form-error">{error}</p>}
        <button className="primary">{t.send}</button>
      </form>
    </section>
  );
}
