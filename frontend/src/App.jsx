import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { getToken } from './api'
import Layout from './core/components/Layout'
import Login from './core/pages/Login'
import Console from './core/pages/Console'
import Reporting from './core/pages/Reporting'
import Problems from './modules/problem-management/pages/Problems'
import ProblemDetail from './modules/problem-management/pages/ProblemDetail'
import Actions from './modules/problem-management/pages/Actions'

function Private({ children }) {
  return getToken() ? children : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<Private><Layout /></Private>}>
        {/* Pilotage — transverse à tous les processus */}
        <Route index element={<Console />} />
        <Route path="reports" element={<Reporting />} />
        {/* Processus ITIL — Gestion des problèmes */}
        <Route path="problems" element={<Problems />} />
        <Route path="problems/:id" element={<ProblemDetail />} />
        <Route path="actions" element={<Actions />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
