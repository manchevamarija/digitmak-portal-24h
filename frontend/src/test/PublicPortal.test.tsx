import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import axe from "axe-core";
import { Header } from "../components/layout/Header";
import { PartnersSection } from "../components/partners/PartnersSection";
import { DmaContactPage } from "../pages/public/DmaContactPage";

describe("public portal", () => {
  it("changes the public navigation language", async () => {
    const onLanguage = vi.fn();
    render(
      <Header
        language="mk"
        view="home"
        onLanguage={onLanguage}
        onNavigate={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByLabelText("Language"), {
      target: { value: "en" },
    });
    expect(onLanguage).toHaveBeenCalledWith("en");
    expect(
      await screen.findByRole("button", { name: "Home" }),
    ).toBeInTheDocument();
  });

  it("renders the DMA form without automated accessibility violations", async () => {
    const { container } = render(<DmaContactPage />);
    const results = await axe.run(container, {
      rules: { "color-contrast": { enabled: false } },
    });
    expect(results.violations).toEqual([]);
  });

  it("renders all partners under the correct heading", () => {
    render(<PartnersSection language="mk" />);
    expect(screen.getByText("НАШИТЕ ПАРТНЕРИ")).toBeInTheDocument();
    expect(screen.getAllByRole("link")).toHaveLength(8);
    expect(screen.queryByText(/конзорциум/i)).not.toBeInTheDocument();
  });
});
