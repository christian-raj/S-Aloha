import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Questionnaire from './Questionnaire'
import Synthesis from './Synthesis'

const answer = vi.fn((id, rid, dto) => Promise.resolve({
  requirementId: rid, ...dto, updatedBy: 'Tiana', updatedAt: '2026-10-08T10:00:00Z', assessmentStatus: 'En cours'
}))
const createImprovement = vi.fn(() => Promise.resolve({ id: 9, reference: 'AMI-2026-0009' }))

vi.mock('../../../api', () => ({
  api: { assessments: {
    questionnaire: () => Promise.resolve({ referentialVersion: 'ReCyF v2.5', objectives: [{
      id: 1, title: 'Recensement des systèmes d\'information', pillar: 'Gouvernance',
      themes: [{ theme: 'Recensement des SI', requirements: [
        { id: 11, code: '1.1-EI/EE', isoControls: '27002:2022-5.9', text: null, score: null, notApplicable: false, justification: '' },
      ] }] }] }),
    answer: (...a) => answer(...a),
    score: () => Promise.resolve({
      percent: 33.3, maturity: { level: 2, name: 'Géré' }, applicable: 76, answered: 1, notApplicable: 0,
      pillars: [{ pillar: 'Gouvernance', percent: 33.3 }],
      objectives: [{ id: 1, title: 'Recensement', pillar: 'Gouvernance', percent: 33.3, applicable: 3, answered: 1 }],
      gaps: [{ requirementId: 11, code: '1.1-EI/EE', theme: 'Recensement des SI', objectiveId: 1, score: 1, gap: 2 }] }),
    createImprovement: (...a) => createImprovement(...a),
  } }
}))

afterEach(() => { cleanup(); answer.mockClear() })

const assessment = (status = 'En cours') => ({ id: 5, status, entityCategory: 'Entité importante' })

describe('Questionnaire NIS 2', () => {
  it('enregistre un score, puis « non applicable »', async () => {
    render(<Questionnaire assessment={assessment()} onChanged={() => {}} />)
    fireEvent.click(await screen.findByText(/Recensement des systèmes/))
    const select = screen.getByLabelText('Réponse 1.1-EI/EE')
    // Sans texte importé, l'utilisateur sait où l'obtenir.
    expect(screen.getByText(/Texte de l'exigence non importé/)).toBeTruthy()

    fireEvent.change(select, { target: { value: '2' } })
    await waitFor(() => expect(answer).toHaveBeenLastCalledWith(5, 11, { score: 2, notApplicable: false, justification: '' }))
    fireEvent.change(select, { target: { value: 'na' } })
    await waitFor(() => expect(answer).toHaveBeenLastCalledWith(5, 11, { score: null, notApplicable: true, justification: '' }))
  })

  it('est en lecture seule une fois l\'évaluation validée', async () => {
    render(<Questionnaire assessment={assessment('Validée')} onChanged={() => {}} />)
    fireEvent.click(await screen.findByText(/Recensement des systèmes/))
    expect(screen.getByLabelText('Réponse 1.1-EI/EE').disabled).toBe(true)
    expect(screen.getByText(/lecture seule/)).toBeTruthy()
  })
})

describe('Synthèse NIS 2', () => {
  it('affiche score et maturité, et transforme un écart en action d\'amélioration', async () => {
    render(<MemoryRouter><Synthesis assessment={assessment()} onChanged={() => {}} /></MemoryRouter>)
    expect(await screen.findByText('2/5')).toBeTruthy()
    fireEvent.click(screen.getByText('Créer une action d\'amélioration'))
    await waitFor(() => expect(createImprovement).toHaveBeenCalledWith(5, '1.1-EI/EE'))
    expect((await screen.findByText('AMI-2026-0009 →')).getAttribute('href')).toBe('/improvements/9')
  })
})
