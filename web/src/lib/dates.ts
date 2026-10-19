
const sastDateTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Africa/Johannesburg',
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

export function formatSastDateTime(
  isoDate: string | null | undefined,
): string {
  if (!isoDate) {
    return 'Not scheduled'
  }

  const date = new Date(isoDate)

  if (Number.isNaN(date.getTime())) {
    throw new RangeError('Invalid date')
  }

  return sastDateTime.format(date)
}
