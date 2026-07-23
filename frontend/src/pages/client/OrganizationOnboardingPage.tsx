import { useState } from "react";
import type { FormEvent } from "react";
import { api, ApiError } from "../../api";
import { useAuth } from "../../features/auth/useAuth";
import { labelFor } from "../../shared/labels";
import type { Navigate } from "../../shared/types";
import { useApiResource } from "../../shared/useApiResource";
import { usePortalLanguage } from "../../shared/usePortalLanguage";

type AvailableOrganization = {
  id: string;
  name: string;
  type: string;
  region?: string;
};

const copy = {
  mk: {
    title: "Организација и членство.",
    lead: "Создајте нова организација или побарајте членство во веќе одобрена организација.",
    create: "Нова организација",
    join: "Приклучи се",
    org: "Организација",
    choose: "Изберете",
    name: "Име на организација",
    type: "Тип",
    sector: "Сектор",
    municipality: "Општина",
    region: "Регион",
    website: "Веб-страница",
    employees: "Број на вработени",
    submit: "Испрати за одобрување",
    error: "Организацијата не може да се зачува.",
    alreadyMember:
      "Веќе сте поврзани со организација. Нема потреба повторно да испраќате барање.",
    pending: "Веќе имате испратено барање за членство во оваа организација.",
    unavailable: "Избраната организација повеќе не е достапна за приклучување.",
    back: "Назад кон порталот",
  },
  en: {
    title: "Organisation and membership.",
    lead: "Create a new organisation or request membership in an approved organisation.",
    create: "New organisation",
    join: "Join",
    org: "Organisation",
    choose: "Choose",
    name: "Organisation name",
    type: "Type",
    sector: "Sector",
    municipality: "Municipality",
    region: "Region",
    website: "Website",
    employees: "Number of employees",
    submit: "Submit for approval",
    error: "The organisation could not be saved.",
    alreadyMember:
      "You are already connected to an organisation. You do not need to submit another request.",
    pending:
      "You have already submitted a membership request for this organisation.",
    unavailable:
      "The selected organisation is no longer available for joining.",
    back: "Back to the portal",
  },
  sq: {
    title: "Organizata dhe anëtarësimi.",
    lead: "Krijoni organizatë të re ose kërkoni anëtarësim në një organizatë të miratuar.",
    create: "Organizatë e re",
    join: "Bashkohu",
    org: "Organizata",
    choose: "Zgjidh",
    name: "Emri i organizatës",
    type: "Lloji",
    sector: "Sektori",
    municipality: "Komuna",
    region: "Rajoni",
    website: "Uebfaqja",
    employees: "Numri i punonjësve",
    submit: "Dërgo për miratim",
    error: "Organizata nuk mund të ruhej.",
    alreadyMember:
      "Tashmë jeni të lidhur me një organizatë. Nuk duhet të dërgoni kërkesë tjetër.",
    pending: "Tashmë keni dërguar kërkesë për anëtarësim në këtë organizatë.",
    unavailable:
      "Organizata e zgjedhur nuk është më e disponueshme për anëtarësim.",
    back: "Kthehu në portal",
  },
};

export function OrganizationOnboardingPage({
  onNavigate,
}: {
  onNavigate: Navigate;
}) {
  const { user } = useAuth();
  const language = usePortalLanguage();
  const text = copy[language];
  const [mode, setMode] = useState<"create" | "join">("create");
  const [error, setError] = useState("");
  const available = useApiResource<AvailableOrganization[]>(
    "/api/organizations/available",
  );

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError("");
    const data = new FormData(event.currentTarget);
    try {
      if (mode === "join") {
        await api(`/api/organizations/${data.get("organizationId")}/join`, {
          method: "POST",
        });
      } else {
        await api("/api/organizations/", {
          method: "POST",
          body: JSON.stringify({
            name: data.get("name"),
            type: data.get("type"),
            sector: data.get("sector"),
            municipality: data.get("municipality"),
            region: data.get("region"),
            website: data.get("website"),
            employeeCount: data.get("employeeCount")
              ? Number(data.get("employeeCount"))
              : null,
          }),
        });
      }
      onNavigate("dashboard");
    } catch (reason) {
      if (
        reason instanceof ApiError &&
        reason.code === "ORGANIZATION_ALREADY_ASSIGNED"
      )
        setError(text.alreadyMember);
      else if (
        reason instanceof ApiError &&
        reason.code === "ORGANIZATION_MEMBERSHIP_EXISTS"
      )
        setError(text.pending);
      else if (
        reason instanceof ApiError &&
        reason.code === "ORGANIZATION_NOT_AVAILABLE"
      )
        setError(text.unavailable);
      else setError(reason instanceof Error ? reason.message : text.error);
    }
  };

  if (user?.organizationId) {
    return (
      <section className="page contact">
        <div>
          <span className="kicker">ONBOARDING</span>
          <h1>{text.title}</h1>
          <p className="lead">{text.alreadyMember}</p>
          <button
            className="primary"
            type="button"
            onClick={() => onNavigate("dashboard")}
          >
            {text.back}
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="page contact">
      <div>
        <span className="kicker">ONBOARDING</span>
        <h1>{text.title}</h1>
        <p className="lead">{text.lead}</p>
        <div className="action-row">
          <button
            className={mode === "create" ? "primary" : "secondary"}
            onClick={() => setMode("create")}
          >
            {text.create}
          </button>
          <button
            className={mode === "join" ? "primary" : "secondary"}
            onClick={() => setMode("join")}
          >
            {text.join}
          </button>
        </div>
      </div>
      <form onSubmit={submit}>
        {mode === "join" ? (
          <label>
            {text.org}
            <select name="organizationId" required>
              <option value="">{text.choose}</option>
              {(available.data ?? []).map((item) => (
                <option value={item.id} key={item.id}>
                  {item.name} · {item.region}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <>
            <label>
              {text.name}
              <input name="name" required />
            </label>
            <div className="row">
              <label>
                {text.type}
                <select name="type">
                  {(
                    [
                      "SME",
                      "MidCap",
                      "PublicInstitution",
                      "Partner",
                      "Other",
                    ] as const
                  ).map((value) => (
                    <option key={value} value={value}>
                      {labelFor(value, language)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                {text.sector}
                <input name="sector" />
              </label>
            </div>
            <div className="row">
              <label>
                {text.municipality}
                <input name="municipality" />
              </label>
              <label>
                {text.region}
                <input name="region" />
              </label>
            </div>
            <div className="row">
              <label>
                {text.website}
                <input name="website" type="url" />
              </label>
              <label>
                {text.employees}
                <input name="employeeCount" type="number" min="1" />
              </label>
            </div>
          </>
        )}
        {error && <p className="form-error">{error}</p>}
        <button className="primary">{text.submit}</button>
      </form>
    </section>
  );
}
