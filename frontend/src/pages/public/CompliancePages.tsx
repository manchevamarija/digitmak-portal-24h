import { uiCopy } from "../../content/uiCopy";
import { usePortalLanguage } from "../../shared/usePortalLanguage";

const legalAddenda = {
  mk: {
    privacy: [
      [
        "Правна основа и согласност",
        "Обработката се врши за обезбедување на побараните услуги, исполнување договорни и законски обврски, легитимна безбедност на порталот и согласност кога таа е потребна.",
      ],
      [
        "Обработувачи и пренос",
        "Хостинг, е-пошта и безбедносни добавувачи смеат да обработуваат податоци само според договор, документирани инструкции и соодветни заштитни мерки.",
      ],
      [
        "Безбедност и инциденти",
        "Порталот применува контрола на пристап, audit записи, шифриран пренос, проверка на прилози и постапка за пријавување и управување со безбедносни инциденти.",
      ],
    ],
    terms: [
      [
        "Интелектуална сопственост",
        "Содржината и материјалите се користат само за дозволените DigitMak услуги, освен ако сопственикот изречно не дозволи поинаку.",
      ],
      [
        "Надворешни услуги",
        "Moodle, календарските и другите надворешни врски може да имаат сопствени услови и политики. DigitMak не ги активира без соодветна конфигурација и одобрување.",
      ],
      [
        "Измени на условите",
        "При материјална измена се објавува нова верзија. Системот ја евидентира прифатената верзија и може повторно да побара согласност.",
      ],
    ],
  },
  en: {
    privacy: [
      [
        "Legal basis and consent",
        "Processing supports requested services, contractual and legal obligations, legitimate portal security and consent where consent is required.",
      ],
      [
        "Processors and transfers",
        "Hosting, email and security providers may process data only under contract, documented instructions and appropriate safeguards.",
      ],
      [
        "Security and incidents",
        "The portal applies access control, audit records, encrypted transport, attachment validation and a procedure for reporting and managing security incidents.",
      ],
    ],
    terms: [
      [
        "Intellectual property",
        "Content and materials may be used only for authorised DigitMak services unless their owner expressly permits otherwise.",
      ],
      [
        "External services",
        "Moodle, calendar and other external links may have separate terms and policies. DigitMak does not activate them without appropriate configuration and approval.",
      ],
      [
        "Changes to these terms",
        "A new version is published for material changes. The system records the accepted version and may request renewed acceptance.",
      ],
    ],
  },
  sq: {
    privacy: [
      [
        "Baza ligjore dhe pëlqimi",
        "Përpunimi mbështet shërbimet e kërkuara, detyrimet kontraktuale dhe ligjore, sigurinë legjitime të portalit dhe pëlqimin kur kërkohet.",
      ],
      [
        "Përpunuesit dhe transferimet",
        "Ofruesit e hostimit, emailit dhe sigurisë mund të përpunojnë të dhëna vetëm me kontratë, udhëzime të dokumentuara dhe masa të përshtatshme.",
      ],
      [
        "Siguria dhe incidentet",
        "Portali zbaton kontroll të qasjes, auditim, transmetim të enkriptuar, verifikim të skedarëve dhe procedurë për menaxhimin e incidenteve.",
      ],
    ],
    terms: [
      [
        "Pronësia intelektuale",
        "Përmbajtja dhe materialet përdoren vetëm për shërbimet e autorizuara DigitMak, përveç nëse pronari lejon shprehimisht ndryshe.",
      ],
      [
        "Shërbimet e jashtme",
        "Moodle, kalendarët dhe lidhjet e jashtme mund të kenë kushte të veçanta. DigitMak nuk i aktivizon pa konfigurim dhe miratim.",
      ],
      [
        "Ndryshimet e kushteve",
        "Për ndryshime thelbësore publikohet version i ri. Sistemi regjistron versionin e pranuar dhe mund të kërkojë pranim të ri.",
      ],
    ],
  },
} as const;

function CompliancePage({
  title,
  sections,
  kind,
}: {
  title: string;
  sections: readonly (readonly [string, string])[];
  kind: "privacy" | "terms";
}) {
  const language = usePortalLanguage();
  const t = uiCopy[language].legal;
  const allSections = [...sections, ...legalAddenda[language][kind]];
  return (
    <section className="page compliance">
      <span className="kicker">{t.kicker}</span>
      <h1>{title}</h1>
      {allSections.map(([heading, body]) => (
        <div key={heading}>
          <h2>{heading}</h2>
          <p>{body}</p>
        </div>
      ))}
    </section>
  );
}

export function PrivacyPage() {
  const t = uiCopy[usePortalLanguage()].legal;
  return (
    <CompliancePage
      title={t.privacyTitle}
      sections={t.privacySections}
      kind="privacy"
    />
  );
}

export function TermsPage() {
  const t = uiCopy[usePortalLanguage()].legal;
  return (
    <CompliancePage
      title={t.termsTitle}
      sections={t.termsSections}
      kind="terms"
    />
  );
}
