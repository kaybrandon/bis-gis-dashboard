/** Send-report / export person label: Full name when on file, otherwise username/display. */
export function reportPersonLabel(person: { displayName: string; fullName?: string | null }) {
  return person.fullName?.trim() || person.displayName
}
