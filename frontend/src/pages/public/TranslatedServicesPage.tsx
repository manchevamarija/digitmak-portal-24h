import { useTranslation } from "react-i18next";
import { useApiResource } from "../../shared/useApiResource";
import type { Navigate } from "../../shared/types";

type Service = {
  id?: string;
  slug: string;
  name?: string;
  fields?: Record<string, string>;
};

const copy = {
  mk: {
    kicker: "КАТАЛОГ НА УСЛУГИ",
    title: "Знаење што се претвора во напредок.",
    lead: "Практична експертска поддршка за компании и јавни институции.",
    action: "Побарај консултација",
    loading: "Се вчитува актуелниот каталог…",
  },
  en: {
    kicker: "SERVICE CATALOGUE",
    title: "Knowledge turned into progress.",
    lead: "Practical expert support for companies and public institutions.",
    action: "Request consultation",
    loading: "Loading the latest catalogue…",
  },
  sq: {
    kicker: "KATALOGU I SHËRBIMEVE",
    title: "Njohuri që shndërrohen në progres.",
    lead: "Mbështetje praktike për kompanitë dhe institucionet publike.",
    action: "Kërko konsultim",
    loading: "Po ngarkohet katalogu…",
  },
};

const fallbackServices = {
  mk: [
    "Подготвеност за вештачка интелигенција",
    "AI Act и усогласеност",
    "Тестирај пред да инвестираш",
    "Дигитална патоказна стратегија",
  ],
  en: [
    "AI Readiness",
    "AI Act & Compliance",
    "Test Before Invest",
    "Digital Roadmap",
  ],
  sq: [
    "Gatishmëria për AI",
    "AI Act dhe pajtueshmëria",
    "Testo para investimit",
    "Udhërrëfyesi digjital",
  ],
};

export function TranslatedServicesPage({
  onNavigate,
}: {
  onNavigate: Navigate;
}) {
  const { i18n } = useTranslation();
  const locale = i18n.language.startsWith("en")
    ? "en"
    : i18n.language.startsWith("sq")
      ? "sq"
      : "mk";
  const text = copy[locale];
  const services = useApiResource<Service[]>(
    `/api/public/services?language=${locale}`,
  );
  const visible: Service[] = services.data?.length
    ? services.data
    : fallbackServices[locale].map((name, index) => ({
        slug: `fallback-${index}`,
        fields: { name },
      }));
  return (
    <section className="page">
      <span className="kicker">{text.kicker}</span>
      <h1>{text.title}</h1>
      <p className="lead">{text.lead}</p>
      <div className="cards wide">
        {visible.map((item, index) => (
          <article key={item.id ?? item.slug}>
            <span className="num">
              {String(index + 1).padStart(2, "0")}
            </span>
            <h3>
              {item.fields?.title ??
                item.fields?.name ??
                item.name ??
                item.slug}
            </h3>
            {item.fields?.description && <p>{item.fields.description}</p>}
            <button onClick={() => onNavigate("contact")}>
              {text.action}
            </button>
          </article>
        ))}
      </div>
      {services.loading && (
        <p className="services-sync-status">{text.loading}</p>
      )}
      {services.error && (
        <p className="services-sync-status">{services.error}</p>
      )}
    </section>
  );
}
