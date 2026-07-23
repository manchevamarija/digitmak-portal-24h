import bau from "../../assets/partners/bau.png";
import bic from "../../assets/partners/bic.png";
import ecnm from "../../assets/partners/ecnm.png";
import mir from "../../assets/partners/mir.png";
import oemvp from "../../assets/partners/oemvp.png";
import rdcPolog from "../../assets/partners/rdc-polog.png";
import seeu from "../../assets/partners/seeu.jpg";
import ukim from "../../assets/partners/ukim.png";
import type { Language } from "../../shared/types";

const copy = {
  mk: {
    kicker: "НАШИТЕ ПАРТНЕРИ",
    title: "Заедно ја градиме дигиталната иднина.",
    lead: "Осум организации со заедничка цел: практична дигитална поддршка достапна низ целата земја.",
    partner: "ПАРТНЕР",
    coordinator: "КООРДИНАТОР",
  },
  en: {
    kicker: "OUR PARTNERS",
    title: "Building the digital future together.",
    lead: "Eight organisations with one shared goal: practical digital support available across the country.",
    partner: "PARTNER",
    coordinator: "COORDINATOR",
  },
  sq: {
    kicker: "PARTNERËT TANË",
    title: "Së bashku ndërtojmë të ardhmen digjitale.",
    lead: "Tetë organizata me një qëllim të përbashkët: mbështetje praktike digjitale në të gjithë vendin.",
    partner: "PARTNER",
    coordinator: "KOORDINATOR",
  },
} as const;

const partners = [
  {
    image: ecnm,
    href: "https://www.mchamber.mk/",
    names: {
      mk: "ECNM — Стопанска комора на Северна Македонија",
      en: "ECNM — Economic Chamber of North Macedonia",
      sq: "ECNM — Oda Ekonomike e Maqedonisë së Veriut",
    },
  },
  {
    image: bau,
    href: "https://bauaccelerator.com/",
    names: {
      mk: "BAU — Бизнис-технолошки акцелератор",
      en: "BAU — Business and Technology Accelerator",
      sq: "BAU — Përshpejtues për Biznes dhe Teknologji",
    },
  },
  {
    image: bic,
    href: "https://bic.seeu.edu.mk/",
    names: {
      mk: "BIC — Бизнис и иновациски центар, Тетово",
      en: "BIC — Business and Innovation Centre, Tetovo",
      sq: "BIC — Qendra për Biznes dhe Inovacion, Tetovë",
    },
  },
  {
    image: mir,
    href: "https://mir.org.mk/",
    names: {
      mk: "MIR — Фондација за менаџмент и индустриско истражување, Скопје",
      en: "MIR — Foundation for Management and Industrial Research, Skopje",
      sq: "MIR — Fondacioni për Menaxhim dhe Kërkime Industriale, Shkup",
    },
  },
  {
    image: rdcPolog,
    href: "https://rdcpolog.mk/en/about-us/",
    names: {
      mk: "RDC Polog — Центар за развој на Полошкиот плански регион",
      en: "RDC Polog — Centre for Development of the Polog Planning Region",
      sq: "RDC Polog — Qendra për Zhvillimin e Rajonit Planor të Pollogut",
    },
  },
  {
    image: oemvp,
    href: "https://oemvp.org/mk",
    names: {
      mk: "OEMVP — Стопанска комора на Северо-западна Македонија",
      en: "OEMVP — Economic Chamber of North-West Macedonia",
      sq: "OEMVP — Oda Ekonomike e Maqedonisë Veri-Perëndimore",
    },
  },
  {
    image: seeu,
    href: "https://www.seeu.edu.mk/",
    names: {
      mk: "SEEU — Универзитет на Југоисточна Европа, Тетово",
      en: "SEEU — South East European University, Tetovo",
      sq: "SEEU — Universiteti i Evropës Juglindore, Tetovë",
    },
  },
  {
    image: ukim,
    href: "https://ukim.edu.mk/",
    coordinator: true,
    names: {
      mk: "UKIM — Универзитет „Св. Кирил и Методиј“ во Скопје",
      en: "UKIM — Ss. Cyril and Methodius University in Skopje",
      sq: "UKIM — Universiteti „Shën Kirili dhe Metodi“ në Shkup",
    },
  },
] as const;

export function PartnersSection({ language }: { language: Language }) {
  const text = copy[language];
  return (
    <section className="partners-section" aria-labelledby="partners-title">
      <header className="partners-heading">
        <div>
          <span className="kicker">{text.kicker}</span>
          <h2 id="partners-title">{text.title}</h2>
        </div>
        <p>{text.lead}</p>
      </header>
      <div className="partner-grid">
        {partners.map((partner, index) => (
          <a
            key={partner.href}
            className="partner-card"
            href={partner.href}
            target="_blank"
            rel="noreferrer"
          >
            <span className="partner-index">0{index + 1}</span>
            <div className="partner-logo">
              <img src={partner.image} alt="" loading="lazy" />
            </div>
            <h3>{partner.names[language]}</h3>
            <span className="partner-role">
              {"coordinator" in partner && partner.coordinator
                ? text.coordinator
                : text.partner}
            </span>
          </a>
        ))}
      </div>
    </section>
  );
}
