import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MeetingWorkspace } from "../features/meetings/MeetingWorkspace";

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock("../api", () => ({
  api: apiMock,
  getAccessToken: () => null,
}));

describe("asynchronous form cleanup", () => {
  beforeEach(() => apiMock.mockReset().mockResolvedValue({}));

  it("clears the meeting form after a successful request without losing the form reference", async () => {
    const onChanged = vi.fn();
    const { container } = render(
      <MeetingWorkspace meetings={[]} onChanged={onChanged} />,
    );
    const subject = screen.getByLabelText("Тема") as HTMLInputElement;
    const description = screen.getByLabelText("Опис") as HTMLTextAreaElement;
    fireEvent.change(subject, { target: { value: "AI консултација" } });
    fireEvent.change(description, { target: { value: "Проверка на процес" } });
    fireEvent.submit(container.querySelector("form")!);

    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
    expect(subject.value).toBe("");
    expect(description.value).toBe("");
    expect(
      screen.queryByText(/Cannot read properties of null/i),
    ).not.toBeInTheDocument();
  });
});
