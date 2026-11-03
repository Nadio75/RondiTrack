import JoinWizard from "./JoinWizard";

type JoinPageProps = {
  searchParams: Promise<{
    code?: string | string[];
    step?: string | string[];
  }>;
};

export default async function JoinPage({
  searchParams,
}: JoinPageProps) {
  const params = await searchParams;

  const code =
    typeof params.code === "string" ? params.code : "";


  return <JoinWizard initialCode={code} />;
}
