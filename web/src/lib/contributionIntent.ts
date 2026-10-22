export type ContributionIntent = {
  fingerprint: string
  key: string
}

export function getContributionIntent(
  previous: ContributionIntent | null,
  fingerprint: string,
): ContributionIntent {
  if (previous?.fingerprint === fingerprint) {
    return previous
  }

  return {
    fingerprint,
    key: crypto.randomUUID(),
  }
}