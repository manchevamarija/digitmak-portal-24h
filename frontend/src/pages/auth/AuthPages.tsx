import { useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { api } from "../../api";
import type { Navigate } from "../../shared/types";
import { usePortalLanguage } from "../../shared/usePortalLanguage";

const copy = {
  mk: {
    lead: "Безбеден пристап до експертска поддршка, тикети и состаноци.",
    registerTitle: "Создајте корисничка сметка.",
    firstName: "Име",
    lastName: "Презиме",
    email: "Е-пошта",
    phone: "Телефон",
    password: "Лозинка",
    language: "Јазик",
    terms: "Ги прифаќам условите за користење и политиката за приватност.",
    register: "Регистрирај се",
    registered: "Регистрацијата е успешна. Проверете ја вашата е-пошта.",
    registerError: "Регистрацијата не успеа.",
    haveAccount: "Веќе имате сметка?",
    login: "Најавете се.",
    forgotTitle: "Обновете ја лозинката.",
    instructions: "Ако адресата постои, испратени се инструкции.",
    sendInstructions: "Испрати инструкции",
    backLogin: "Назад кон најава",
    resetTitle: "Поставете нова лозинка.",
    emailCode: "Код од е-пошта",
    newPassword: "Нова лозинка",
    passwordChanged: "Лозинката е променета.",
    savePassword: "Зачувај лозинка",
    verifyTitle: "Потврдете ја е-поштата.",
    verifyLead: "Со потврдата ја активирате вашата DigitMak корисничка сметка.",
    verified: "Е-поштата е потврдена. Сега може да се најавите.",
    invalidLink: "Линкот е невалиден или истечен.",
    verify: "Потврди е-пошта",
    toLogin: "Кон најава",
  },
  en: {
    lead: "Secure access to expert support, tickets and meetings.",
    registerTitle: "Create your account.",
    firstName: "First name",
    lastName: "Last name",
    email: "Email",
    phone: "Phone",
    password: "Password",
    language: "Language",
    terms: "I accept the terms of use and privacy policy.",
    register: "Create account",
    registered: "Registration succeeded. Check your email.",
    registerError: "Registration failed.",
    haveAccount: "Already have an account?",
    login: "Sign in.",
    forgotTitle: "Recover your password.",
    instructions: "If the address exists, instructions have been sent.",
    sendInstructions: "Send instructions",
    backLogin: "Back to sign in",
    resetTitle: "Set a new password.",
    emailCode: "Code from email",
    newPassword: "New password",
    passwordChanged: "Password changed.",
    savePassword: "Save password",
    verifyTitle: "Verify your email.",
    verifyLead: "Verification activates your DigitMak account.",
    verified: "Email verified. You can now sign in.",
    invalidLink: "The link is invalid or expired.",
    verify: "Verify email",
    toLogin: "Go to sign in",
  },
  sq: {
    lead: "Qasje e sigurt në mbështetje profesionale, tiketa dhe takime.",
    registerTitle: "Krijoni llogarinë tuaj.",
    firstName: "Emri",
    lastName: "Mbiemri",
    email: "Email",
    phone: "Telefoni",
    password: "Fjalëkalimi",
    language: "Gjuha",
    terms: "I pranoj kushtet e përdorimit dhe politikën e privatësisë.",
    register: "Krijo llogari",
    registered: "Regjistrimi pati sukses. Kontrolloni emailin.",
    registerError: "Regjistrimi dështoi.",
    haveAccount: "Keni llogari?",
    login: "Hyni.",
    forgotTitle: "Rikuperoni fjalëkalimin.",
    instructions: "Nëse adresa ekziston, udhëzimet janë dërguar.",
    sendInstructions: "Dërgo udhëzimet",
    backLogin: "Kthehu te hyrja",
    resetTitle: "Vendosni fjalëkalim të ri.",
    emailCode: "Kodi nga emaili",
    newPassword: "Fjalëkalimi i ri",
    passwordChanged: "Fjalëkalimi u ndryshua.",
    savePassword: "Ruaj fjalëkalimin",
    verifyTitle: "Konfirmoni emailin.",
    verifyLead: "Konfirmimi aktivizon llogarinë tuaj DigitMak.",
    verified: "Emaili u konfirmua. Tani mund të hyni.",
    invalidLink: "Lidhja është e pavlefshme ose ka skaduar.",
    verify: "Konfirmo emailin",
    toLogin: "Shko te hyrja",
  },
} as const;

function AuthShell({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  const t = copy[usePortalLanguage()];
  return (
    <section className="page split">
      <div>
        <span className="kicker">DIGITMAK PORTAL</span>
        <h1>{title}</h1>
        <p className="lead">{t.lead}</p>
      </div>
      {children}
    </section>
  );
}

export function RegisterPage({ onNavigate }: { onNavigate: Navigate }) {
  const t = copy[usePortalLanguage()];
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setError("");
    try {
      await api("/api/auth/register", {
        method: "POST",
        body: JSON.stringify({
          email: data.get("email"),
          password: data.get("password"),
          firstName: data.get("firstName"),
          lastName: data.get("lastName"),
          preferredLanguage: data.get("language"),
          phone: data.get("phone"),
          termsAccepted: data.get("terms") === "on",
          termsVersion: "terms-2026-07-v1",
          privacyVersion: "privacy-2026-07-v1",
        }),
      });
      setMessage(t.registered);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.registerError);
    }
  };
  return (
    <AuthShell title={t.registerTitle}>
      <form className="login-card" onSubmit={submit}>
        <div className="row">
          <label>
            {t.firstName}
            <input name="firstName" required />
          </label>
          <label>
            {t.lastName}
            <input name="lastName" required />
          </label>
        </div>
        <label>
          {t.email}
          <input name="email" type="email" required />
        </label>
        <label>
          {t.phone}
          <input name="phone" type="tel" />
        </label>
        <label>
          {t.password}
          <input
            name="password"
            type="password"
            minLength={10}
            autoComplete="new-password"
            required
          />
          <small>
            {usePortalLanguage() === "mk"
              ? "Најмалку 10 знаци, со голема и мала буква, број и специјален знак."
              : "At least 10 characters with upper and lower case letters, a number and a special character."}
          </small>
        </label>
        <label>
          {t.language}
          <select name="language">
            <option value="mk">Македонски</option>
            <option value="en">English</option>
            <option value="sq">Shqip</option>
          </select>
        </label>
        <label className="check">
          <input name="terms" type="checkbox" required /> {t.terms}
        </label>
        {message && <p className="notice">{message}</p>}
        {error && <p className="form-error">{error}</p>}
        <button className="primary">{t.register}</button>
        <small>
          {t.haveAccount}{" "}
          <button type="button" onClick={() => onNavigate("help")}>
            {t.login}
          </button>
        </small>
      </form>
    </AuthShell>
  );
}

export function ForgotPasswordPage({ onNavigate }: { onNavigate: Navigate }) {
  const t = copy[usePortalLanguage()];
  const [done, setDone] = useState(false);
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    await api("/api/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify({ email: data.get("email") }),
    });
    setDone(true);
  };
  return (
    <AuthShell title={t.forgotTitle}>
      <form className="login-card" onSubmit={submit}>
        <label>
          {t.email}
          <input name="email" type="email" required />
        </label>
        {done && <p className="notice">{t.instructions}</p>}
        <button className="primary">{t.sendInstructions}</button>
        <small>
          <button type="button" onClick={() => onNavigate("help")}>
            {t.backLogin}
          </button>
        </small>
      </form>
    </AuthShell>
  );
}

export function ResetPasswordPage({ onNavigate }: { onNavigate: Navigate }) {
  const t = copy[usePortalLanguage()];
  const [done, setDone] = useState(false);
  const query = new URLSearchParams(window.location.search);
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    await api("/api/auth/reset-password", {
      method: "POST",
      body: JSON.stringify({
        email: data.get("email"),
        token: data.get("token"),
        newPassword: data.get("password"),
      }),
    });
    setDone(true);
  };
  return (
    <AuthShell title={t.resetTitle}>
      <form className="login-card" onSubmit={submit}>
        <label>
          {t.email}
          <input
            name="email"
            type="email"
            required
            defaultValue={query.get("email") ?? ""}
          />
        </label>
        <label>
          {t.emailCode}
          <input
            name="token"
            required
            defaultValue={query.get("token") ?? ""}
          />
        </label>
        <label>
          {t.newPassword}
          <input name="password" type="password" minLength={10} required />
        </label>
        {done && <p className="notice">{t.passwordChanged}</p>}
        <button className="primary">{t.savePassword}</button>
        <small>
          <button type="button" onClick={() => onNavigate("help")}>
            {t.login}
          </button>
        </small>
      </form>
    </AuthShell>
  );
}

export function VerifyEmailPage({ onNavigate }: { onNavigate: Navigate }) {
  const t = copy[usePortalLanguage()];
  const [state, setState] = useState<"idle" | "done" | "error">("idle");
  const verify = async () => {
    const query = new URLSearchParams(window.location.search);
    try {
      await api("/api/auth/verify-email", {
        method: "POST",
        body: JSON.stringify({
          userId: query.get("userId"),
          token: query.get("token"),
        }),
      });
      setState("done");
    } catch {
      setState("error");
    }
  };
  return (
    <AuthShell title={t.verifyTitle}>
      <div className="login-card">
        <p>{t.verifyLead}</p>
        {state === "done" && <p className="notice">{t.verified}</p>}
        {state === "error" && <p className="form-error">{t.invalidLink}</p>}
        {state !== "done" && (
          <button className="primary" onClick={verify}>
            {t.verify}
          </button>
        )}
        <button className="secondary" onClick={() => onNavigate("help")}>
          {t.toLogin}
        </button>
      </div>
    </AuthShell>
  );
}
