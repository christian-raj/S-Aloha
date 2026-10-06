import React, { useState } from 'react'
import { afterEach, expect, it } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import FtaTree from './FtaTree'

afterEach(cleanup)

function Harness() {
  const [data, setData] = useState({})
  return <FtaTree data={data} onChange={setData} />
}

// M1, revue du 2026-10-06 : le champ était démonté et recréé à chaque frappe,
// la saisie s'arrêtait au premier caractère. On vérifie que l'élément édité
// est TOUJOURS le même nœud du DOM après la frappe.
it('la saisie dans une cause ne démonte pas le champ', () => {
  render(<Harness />)
  fireEvent.click(screen.getByText('+ cause'))
  const field = screen.getByPlaceholderText('Cause ou événement intermédiaire')
  field.focus()

  fireEvent.change(field, { target: { value: 'D' } })
  fireEvent.change(field, { target: { value: 'Disque plein' } })

  expect(field.isConnected).toBe(true)
  expect(document.activeElement).toBe(field)
  expect(field.value).toBe('Disque plein')
})
