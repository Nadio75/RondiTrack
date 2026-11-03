export type InvitationPreview = {
  stokvelId: string;
  name: string;
  rules: string;
  contributionAmount: number;
  closingDayOfMonth: number;
  memberCount: number;
};

export type JoinWizardState =
  | {
      step: "code";
      code: string;
    }
  | {
      step: "rules";
      code: string;
      invitation: InvitationPreview;
    }
  | {
      step: "confirm";
      code: string;
      invitation: InvitationPreview;
    }
  | {
      step: "success";
      stokvelId: string;
    };

export type JoinWizardAction =
  | { type: "ENTER_CODE"; code: string }
  | {
      type: "PREVIEW_LOADED";
      code: string;
      invitation: InvitationPreview;
    }
  | { type: "NAVIGATE"; step: "code" | "rules" | "confirm" }
  | { type: "CONTINUE" }
  | { type: "BACK" }
  | { type: "JOINED"; stokvelId: string };

export function joinWizardReducer(
  state: JoinWizardState,
  action: JoinWizardAction,
): JoinWizardState {
  switch (action.type) {
    case "ENTER_CODE":
      return {
        step: "code",
        code: action.code,
      };

    case "PREVIEW_LOADED":
      return {
        step: "rules",
        code: action.code,
        invitation: action.invitation,
      };

    case "NAVIGATE":
      if (action.step === "code") {
        return {
          step: "code",
          code: state.step === "success" ? "" : state.code,
        };
      }

      if (state.step === "rules" || state.step === "confirm") {
        return {
          step: action.step,
          code: state.code,
          invitation: state.invitation,
        };
      }

      return state;

    case "CONTINUE":
      if (state.step !== "rules") return state;

      return {
        step: "confirm",
        code: state.code,
        invitation: state.invitation,
      };

    case "BACK":
      if (state.step === "confirm") {
        return {
          step: "rules",
          code: state.code,
          invitation: state.invitation,
        };
      }

      if (state.step === "rules") {
        return {
          step: "code",
          code: state.code,
        };
      }

      return state;

    case "JOINED":
      if (state.step !== "confirm") return state;

      return {
        step: "success",
        stokvelId: action.stokvelId,
      };

    default:
      return state;
  }
}
