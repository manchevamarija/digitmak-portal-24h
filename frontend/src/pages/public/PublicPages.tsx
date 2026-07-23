import { useState } from "react";
import type { FormEvent } from "react";
import { api, ApiError } from "../../api";
import { copy, services } from "../../content/portalContent";
import { useAuth } from "../../features/auth/useAuth";
import { uiCopy } from "../../content/uiCopy";
import { usePortalLanguage } from "../../shared/usePortalLanguage";
import type { Language, Navigate } from "../../shared/types";
import { PartnersSection } from "../../components/partners/PartnersSection";

export function HomePage({
  language,
  onNavigate,
}: {
  language: Language;
  onNavigate: Navigate;
}) {
  const t = copy[language];
  return (
    <>
      <section className="hero">
        <div className="hero-copy">
          <div className="eyebrow">
            <span /> European Digital Innovation Hub
          </div>
          <h1>
            {t.heroLead} <em>{t.heroAccent}</em>
          </h1>
          <p>{t.sub}</p>
          <div className="actions">
            <button className="primary" onClick={() => onNavigate("contact")}>
              {t.start}
            </button>
            <button
              className="secondary"
              onClick={() => onNavigate("services")}
            >
              {t.explore}
            </button>
          </div>
        </div>
        <div className="hero-network" aria-hidden="true">
          <span className="network-core">AI</span>
          <i className="network-node node-one" />
          <i className="network-node node-two" />
          <i className="network-node node-three" />
          {(t.heroSignals as string[]).map((signal, index) => (
            <div className={`hero-signal signal-${index + 1}`} key={signal}>
              <span>{index === 0 ? "AI" : index === 1 ? "T" : "1"}</span>
              {signal}
            </div>
          ))}
        </div>
      </section>
      <section className="trust">
        <span>{t.audience}</span>
        <div>
          <b>3</b> {t.languages}
        </div>
        <div>
          <b>12</b> {t.supportMonths}
        </div>
        <div>
          <b>1</b> {t.singlePortal}
        </div>
      </section>
      <section className="service-section">
        <div className="section-head">
          <div>
            <span className="kicker">{t.offerKicker}</span>
            <h2>{t.services}</h2>
          </div>
          <p>{t.offerLead}</p>
        </div>
        <ServiceCards language={language} onNavigate={onNavigate} />
      </section>
      <PartnersSection language={language} />
      <section className="help-strip">
        <div>
          <span className="live" /> AI HELP DESK
        </div>
        <h2>
          {t.helpTitle.split("\n").map((line, index) => (
            <span key={line}>
              {index > 0 && <br />}
              {line}
            </span>
          ))}
        </h2>
        <p>{t.helpLead}</p>
        <button className="light" onClick={() => onNavigate("help")}>
          {t.openTicket}
        </button>
      </section>
    </>
  );
}

function ServiceCards({
  onNavigate,
  language,
  wide = false,
}: {
  onNavigate: Navigate;
  language: Language;
  wide?: boolean;
}) {
  const t = copy[language];
  const baseServices = services[language];
  const items = wide
    ? [
        ...baseServices,
        [
          "Data governance",
          "Подобри податоци, приватност и управување.",
          "◎",
        ] as const,
        [
          "Training & skills",
          "Обуки и ресурси приспособени на вашиот тим.",
          "+",
        ] as const,
      ]
    : baseServices;
  return (
    <div className={`cards${wide ? " wide" : ""}`}>
      {items.map((service, index) => (
        <article key={service[0]}>
          <span className="num">0{index + 1}</span>
          <div className="icon">{service[2]}</div>
          <h3>{service[0]}</h3>
          <p>{service[1]}</p>
          <button onClick={() => onNavigate(wide ? "contact" : "services")}>
            {wide ? "Побарај консултација" : t.learnMore}
          </button>
        </article>
      ))}
    </div>
  );
}

export function ServicesPage({ onNavigate }: { onNavigate: Navigate }) {
  const language = usePortalLanguage();
  return (
    <section className="page">
      <span className="kicker">КАТАЛОГ НА УСЛУГИ</span>
      <h1>Знаење што се претвора во напредок.</h1>
      <p className="lead">
        Практична експертска поддршка за компаниите и јавните институции во
        Северна Македонија.
      </p>
      <ServiceCards language={language} onNavigate={onNavigate} wide />
    </section>
  );
}

export function HelpDeskPage({ onNavigate }: { onNavigate: Navigate }) {
  const { login } = useAuth();
  const language = usePortalLanguage();
  const t = uiCopy[language].help;
  const [error, setError] = useState("");
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setError("");
    try {
      await login(String(data.get("email")), String(data.get("password")));
      onNavigate(
        sessionStorage.getItem("digitmak.subscriptionToken")
          ? "subscription-invite"
          : "dashboard",
      );
    } catch (value) {
      if (value instanceof ApiError && value.status === 401)
        setError(t.invalidCredentials);
      else if (
        value instanceof ApiError &&
        (value.status === 0 || value.status >= 500)
      )
        setError(t.serverUnavailable);
      else if (value instanceof ApiError && value.status === 403)
        setError(t.accountUnavailable);
      else setError(value instanceof Error ? value.message : t.error);
    }
  };
  return (
    <section className="page split">
      <div>
        <span className="kicker">AI HELP DESK</span>
        <h1>{t.title}</h1>
        <p className="lead">{t.lead}</p>
        <div className="notice ai-help-notice">{t.notice}</div>
      </div>
      <form className="login-card" onSubmit={submit}>
        <span>{t.access}</span>
        <h2>{t.login}</h2>
        <label>
          {t.email}
          <input
            name="email"
            required
            type="email"
            placeholder="ime@kompanija.mk"
          />
        </label>
        <label>
          {t.password}
          <input
            name="password"
            required
            type="password"
            placeholder="••••••••"
          />
        </label>
        {error && (
          <p role="alert" className="form-error">
            {error}
          </p>
        )}
        <button className="primary">{t.submit}</button>
        <small>
          {t.noAccount}{" "}
          <button type="button" onClick={() => onNavigate("register")}>
            {t.register}
          </button>
        </small>
        <small>
          <button type="button" onClick={() => onNavigate("forgot")}>
            {t.forgot}
          </button>
        </small>
      </form>
    </section>
  );
}

export function ContactPage() {
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setError("");
    try {
      await api("/api/public/contact-requests", {
        method: "POST",
        body: JSON.stringify({
          organizationName: data.get("organization"),
          organizationType: data.get("organizationType"),
          contactName: data.get("contactName"),
          email: data.get("email"),
          mainNeed: data.get("mainNeed"),
          challengeDescription: data.get("challenge"),
          consentToContact: true,
          privacyPolicyAccepted: true,
        }),
      });
      setSent(true);
    } catch (value) {
      setError(
        value instanceof Error
          ? value.message
          : "Барањето не може да се испрати.",
      );
    }
  };
  return (
    <section className="page contact">
      <div>
        <span className="kicker">КОНТАКТ</span>
        <h1>Кажете ни што сакате да постигнете.</h1>
        <p className="lead">
          Кратка почетна проценка — нашиот тим ќе ви одговори со соодветен
          следен чекор.
        </p>
        <div className="contact-meta">
          <p>Одговор во рок од 2 работни дена</p>
          <p>Достапно на македонски, англиски и албански</p>
        </div>
      </div>
      {sent ? (
        <div className="success">
          <b>✓</b>
          <h2>Барањето е примено.</h2>
          <p>Нашиот тим ќе ве контактира наскоро.</p>
          <button className="secondary" onClick={() => setSent(false)}>
            Ново барање
          </button>
        </div>
      ) : (
        <form onSubmit={submit}>
          <div className="row">
            <label>
              Организација
              <input
                name="organization"
                required
                placeholder="Име на организација"
              />
            </label>
            <label>
              Тип
              <select name="organizationType" required>
                <option>МСП</option>
                <option>Јавна институција</option>
                <option>Партнер</option>
                <option>Друго</option>
              </select>
            </label>
          </div>
          <div className="row">
            <label>
              Име и презиме
              <input name="contactName" required />
            </label>
            <label>
              Е-пошта
              <input name="email" required type="email" />
            </label>
          </div>
          <label>
            Главна потреба
            <select name="mainNeed">
              <option>Вештачка интелигенција</option>
              <option>Дигитализација</option>
              <option>Обука</option>
              <option>Финансирање</option>
              <option>Test-before-invest</option>
            </select>
          </label>
          <label>
            Предизвик
            <textarea
              name="challenge"
              required
              rows={5}
              placeholder="Опишете го вашиот деловен или јавен предизвик..."
            />
          </label>
          <label className="check">
            <input required type="checkbox" /> Се согласувам да бидам
            контактиран/а и ја прифаќам политиката за приватност.
          </label>
          {error && (
            <p role="alert" className="form-error">
              {error}
            </p>
          )}
          <button className="primary">Испрати барање</button>
        </form>
      )}
    </section>
  );
}
