import type { DocumentDifficulty, DocumentDifficultyBand } from './api'

export const DIFFICULTY_BANDS: DocumentDifficultyBand[] = ['Easy', 'Medium', 'Hard']

export function difficultyTagColor(band?: string | null): string {
  if (band === 'Easy') return 'green'
  if (band === 'Medium') return 'gold'
  if (band === 'Hard') return 'red'
  return 'default'
}

export function difficultyReasons(difficulty?: Pick<DocumentDifficulty, 'why' | 'reasons'> | null): string[] {
  if (!difficulty) return []
  if (difficulty.reasons?.length) {
    return difficulty.reasons.map((line) => line.trim()).filter(Boolean).slice(0, 3)
  }
  if (!difficulty.why?.trim()) return []
  return difficulty.why
    .split(/\n|•|;/)
    .map((line) => line.replace(/^[-*]\s*/, '').trim())
    .filter(Boolean)
    .slice(0, 3)
}

export function difficultyWhyText(difficulty?: Pick<DocumentDifficulty, 'why' | 'reasons'> | null): string {
  const reasons = difficultyReasons(difficulty)
  return reasons.join(' ')
}

export function isDifficultyBand(value: string | null | undefined): value is DocumentDifficultyBand {
  return value === 'Easy' || value === 'Medium' || value === 'Hard'
}
