// @vitest-environment jsdom
import { describe, expect, it, vi } from "vitest";
import { recordContribution } from "./actions";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

vi.mock("./actions", () => ({
  recordContribution: vi.fn(),
}));

import { ContributionForm } from "./contribution-form";

const stokvelId = "11111111-1111-4111-8111-111111111111";
const cycleId = "22222222-2222-4222-8222-222222222222";

describe("ContributionForm", () => {
  it("renders an accessible amount field", () => {
    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    const amount = screen.getByRole("textbox", {
      name: /amount/i,
    });

    expect(amount).toBeInTheDocument();
    expect(amount).toHaveAttribute("inputmode", "decimal");
    expect(amount).toBeRequired();
  });

  it("renders the contribution submit button", () => {
    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    expect(
      screen.getByRole("button", {
        name: /record contribution/i,
      }),
    ).toBeEnabled();
  });

  it("allows South African currency input", async () => {
    const user = userEvent.setup();

    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    const amount = screen.getByRole("textbox", {
      name: /amount/i,
    });

    await user.type(amount, "R 1 500,50");

    expect(amount).toHaveValue("R 1 500,50");
  });

  it("shows a field error and preserves the amount", async () => {
    vi.mocked(recordContribution).mockResolvedValueOnce({
      status: "error",
      message: "Invalid amount.",
      submittedAmount: "12,345",
      fieldErrors: {
        amount: "Invalid amount.",
      },
    });

    const user = userEvent.setup();

    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    const amount = screen.getByRole("textbox", {
      name: /amount/i,
    });

    await user.type(amount, "12,345");
    await user.click(
      screen.getByRole("button", {
        name: /record contribution/i,
      }),
    );

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(amount).toHaveValue("12,345");
    expect(amount).toHaveAttribute("aria-invalid", "true");
    expect(amount).toHaveAttribute(
      "aria-describedby",
      "contribution-amount-error",
    );
    expect(
      screen.getByText("Invalid amount.", {
        selector: "#contribution-amount-error",
      }),
    ).toBeInTheDocument();
  });

  it("displays the API reference after a contribution conflict", async () => {
    vi.mocked(recordContribution).mockResolvedValueOnce({
      status: "error",
      message: "This contribution conflicts with an existing payment.",
      correlationId: "REF-409",
      submittedAmount: "150,50",
    });

    const user = userEvent.setup();

    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    const amount = screen.getByRole("textbox", {
      name: /amount/i,
    });

    await user.type(amount, "150,50");
    await user.click(
      screen.getByRole("button", {
        name: /record contribution/i,
      }),
    );

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(amount).toHaveValue("150,50");
    expect(screen.getByText("Reference: REF-409")).toBeInTheDocument();
  });

  it("disables submission while a contribution is processing", async () => {
    let finishPayment!: (value: {
      status: "success";
      message: string;
    }) => void;

    const pendingPayment = new Promise<{
      status: "success";
      message: string;
    }>((resolve) => {
      finishPayment = resolve;
    });

    vi.mocked(recordContribution).mockReturnValueOnce(pendingPayment);

    const user = userEvent.setup();

    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    await user.type(
      screen.getByRole("textbox", { name: /amount/i }),
      "150,50",
    );

    await user.click(
      screen.getByRole("button", {
        name: /record contribution/i,
      }),
    );

    const pendingButton = screen.getByRole("button", {
      name: /recording/i,
    });

    expect(pendingButton).toBeDisabled();
    expect(recordContribution).toHaveBeenCalledTimes(1);

    finishPayment({
      status: "success",
      message: "Contribution recorded successfully.",
    });

    await waitFor(() => {
      expect(
        screen.getByRole("button", {
          name: /record contribution/i,
        }),
      ).toBeEnabled();
    });

    expect(recordContribution).toHaveBeenCalledTimes(1);
  });

  it("clears the amount after a successful contribution", async () => {
    vi.mocked(recordContribution).mockResolvedValueOnce({
      status: "success",
      message: "Contribution recorded successfully.",
    });

    const user = userEvent.setup();

    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    const amount = screen.getByRole("textbox", {
      name: /amount/i,
    });

    await user.type(amount, "150,50");

    expect(amount).toHaveValue("150,50");

    await user.click(
      screen.getByRole("button", {
        name: /record contribution/i,
      }),
    );

    expect(
      await screen.findByRole("status"),
    ).toHaveTextContent("Contribution recorded successfully.");

    await waitFor(() => {
      expect(amount).toHaveValue("");
    });
  });

  it("prevents a second submission while payment is pending", async () => {
    let finishPayment!: (value: {
      status: "success";
      message: string;
    }) => void;

    const pendingPayment = new Promise<{
      status: "success";
      message: string;
    }>((resolve) => {
      finishPayment = resolve;
    });

    vi.mocked(recordContribution).mockReturnValueOnce(pendingPayment);

    const user = userEvent.setup();

    render(
      <ContributionForm stokvelId={stokvelId} cycleId={cycleId} />,
    );

    await user.type(
      screen.getByRole("textbox", { name: /amount/i }),
      "150,50",
    );

    await user.click(
      screen.getByRole("button", {
        name: /record contribution/i,
      }),
    );

    const pendingButton = screen.getByRole("button", {
      name: /recording/i,
    });

    expect(pendingButton).toBeDisabled();

    await user.click(pendingButton);

    expect(recordContribution).toHaveBeenCalledTimes(1);

    finishPayment({
      status: "success",
      message: "Contribution recorded successfully.",
    });

    expect(
      await screen.findByRole("status"),
    ).toHaveTextContent("Contribution recorded successfully.");

    expect(recordContribution).toHaveBeenCalledTimes(1);
  });
});
