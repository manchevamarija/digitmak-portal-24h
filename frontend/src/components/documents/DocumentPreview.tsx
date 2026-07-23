import { useEffect, useMemo, useState } from "react";
import { getAccessToken } from "../../api";
import { usePortalLanguage } from "../../shared/usePortalLanguage";

export type PreviewDocument = {
  fileId: string;
  originalFilename: string;
  contentType: string;
};

const copy = {
  mk: {
    title: "Преглед на документ",
    close: "Затвори",
    download: "Преземи",
    loading: "Документот се вчитува...",
    unavailable: "Документот не може да се прикаже.",
    unsupported:
      "Овој формат нема сигурен преглед во прелистувач. Документот може да се преземе и отвори во соодветна програма.",
  },
  en: {
    title: "Document preview",
    close: "Close",
    download: "Download",
    loading: "Loading document...",
    unavailable: "The document could not be displayed.",
    unsupported:
      "This format has no reliable browser preview. Download it and open it in the appropriate application.",
  },
  sq: {
    title: "Shikimi i dokumentit",
    close: "Mbyll",
    download: "Shkarko",
    loading: "Dokumenti po ngarkohet...",
    unavailable: "Dokumenti nuk mund të shfaqet.",
    unsupported:
      "Ky format nuk ka shikim të sigurt në shfletues. Shkarkojeni dhe hapeni në aplikacionin përkatës.",
  },
} as const;

function parseCsv(value: string) {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let quoted = false;
  for (let index = 0; index < value.length; index += 1) {
    const character = value[index];
    if (character === '"') {
      if (quoted && value[index + 1] === '"') {
        cell += '"';
        index += 1;
      } else quoted = !quoted;
    } else if (character === "," && !quoted) {
      row.push(cell);
      cell = "";
    } else if ((character === "\n" || character === "\r") && !quoted) {
      if (character === "\r" && value[index + 1] === "\n") index += 1;
      row.push(cell);
      if (row.some((item) => item.length)) rows.push(row);
      row = [];
      cell = "";
    } else cell += character;
  }
  row.push(cell);
  if (row.some((item) => item.length)) rows.push(row);
  return rows;
}

export function DocumentPreview({
  document,
  onClose,
}: {
  document: PreviewDocument;
  onClose: () => void;
}) {
  const language = usePortalLanguage();
  const t = copy[language];
  const [blob, setBlob] = useState<Blob>();
  const [text, setText] = useState("");
  const [error, setError] = useState("");
  const objectUrl = useMemo(
    () => (blob ? URL.createObjectURL(blob) : ""),
    [blob],
  );

  useEffect(() => {
    const token = getAccessToken();
    fetch(`/api/files/${document.fileId}`, {
      credentials: "include",
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
      .then(async (response) => {
        if (!response.ok) throw new Error(t.unavailable);
        const value = await response.blob();
        setBlob(value);
        if (
          document.contentType === "text/plain" ||
          document.contentType === "text/csv"
        )
          setText(await value.text());
      })
      .catch(() => setError(t.unavailable));
  }, [document, t.unavailable]);

  useEffect(
    () => () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    },
    [objectUrl],
  );

  const download = () => {
    if (!objectUrl) return;
    const link = window.document.createElement("a");
    link.href = objectUrl;
    link.download = document.originalFilename;
    link.click();
  };
  const csv = document.contentType === "text/csv" ? parseCsv(text) : [];

  return (
    <div
      className="document-preview-backdrop"
      role="presentation"
      onMouseDown={onClose}
    >
      <section
        aria-label={t.title}
        aria-modal="true"
        className="document-preview-modal"
        role="dialog"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header>
          <div>
            <span className="kicker">{t.title}</span>
            <h2>{document.originalFilename}</h2>
          </div>
          <button className="secondary" type="button" onClick={onClose}>
            {t.close}
          </button>
        </header>
        <div className="document-preview-body">
          {!blob && !error && <p>{t.loading}</p>}
          {error && <p className="form-error">{error}</p>}
          {blob && document.contentType === "application/pdf" && (
            <iframe src={objectUrl} title={document.originalFilename} />
          )}
          {blob && document.contentType.startsWith("image/") && (
            <img src={objectUrl} alt={document.originalFilename} />
          )}
          {blob && document.contentType === "text/plain" && <pre>{text}</pre>}
          {blob && document.contentType === "text/csv" && (
            <div className="csv-preview-table-wrap">
              <table className="csv-preview-table">
                <thead>
                  <tr>
                    {(csv[0] ?? []).map((cell, index) => (
                      <th key={index}>{cell}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {csv.slice(1).map((row, rowIndex) => (
                    <tr key={rowIndex}>
                      {row.map((cell, cellIndex) => (
                        <td key={cellIndex}>{cell}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {blob &&
            !["application/pdf", "text/plain", "text/csv"].includes(
              document.contentType,
            ) &&
            !document.contentType.startsWith("image/") && (
              <p className="empty-state">{t.unsupported}</p>
            )}
        </div>
        <footer>
          <button
            className="secondary"
            type="button"
            onClick={download}
            disabled={!blob}
          >
            {t.download}
          </button>
        </footer>
      </section>
    </div>
  );
}
