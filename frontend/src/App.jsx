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
import Incidents from './modules/incident-management/pages/Incidents'
import IncidentDetail from './modules/incident-management/pages/IncidentDetail'
import Requests from './modules/service-request-management/pages/Requests'
import RequestDetail from './modules/service-request-management/pages/RequestDetail'
import Changes from './modules/change-enablement/pages/Changes'
import ChangeDetail from './modules/change-enablement/pages/ChangeDetail'
import ChangeSchedule from './modules/change-enablement/pages/ChangeSchedule'
import ConfigurationItems from './modules/service-configuration-management/pages/ConfigurationItems'
import ConfigurationItemDetail from './modules/service-configuration-management/pages/ConfigurationItemDetail'
import Services from './modules/service-level-management/pages/Services'
import ServiceDetail from './modules/service-level-management/pages/ServiceDetail'
import Agreements from './modules/service-level-management/pages/Agreements'
import AgreementDetail from './modules/service-level-management/pages/AgreementDetail'
import Articles from './modules/knowledge-management/pages/Articles'
import ArticleDetail from './modules/knowledge-management/pages/ArticleDetail'
import Improvements from './modules/continual-improvement/pages/Improvements'
import ImprovementDetail from './modules/continual-improvement/pages/ImprovementDetail'

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
        {/* Pratique — Gestion des incidents */}
        <Route path="incidents" element={<Incidents />} />
        <Route path="incidents/:id" element={<IncidentDetail />} />
        {/* Pratique — Gestion des demandes de service */}
        <Route path="requests" element={<Requests />} />
        <Route path="requests/:id" element={<RequestDetail />} />
        {/* Pratique — Gestion des problèmes */}
        <Route path="problems" element={<Problems />} />
        <Route path="problems/:id" element={<ProblemDetail />} />
        <Route path="actions" element={<Actions />} />
        {/* Pratique — Habilitation des changements */}
        <Route path="changes" element={<Changes />} />
        <Route path="changes/schedule" element={<ChangeSchedule />} />
        <Route path="changes/:id" element={<ChangeDetail />} />
        {/* Pratique — Gestion de la configuration des services */}
        <Route path="configuration" element={<ConfigurationItems />} />
        <Route path="configuration/:id" element={<ConfigurationItemDetail />} />
        {/* Pratique — Gestion des niveaux de service */}
        <Route path="services" element={<Services />} />
        <Route path="services/:id" element={<ServiceDetail />} />
        <Route path="agreements" element={<Agreements />} />
        <Route path="agreements/:id" element={<AgreementDetail />} />
        {/* Pratique — Gestion des connaissances */}
        <Route path="knowledge" element={<Articles />} />
        <Route path="knowledge/:id" element={<ArticleDetail />} />
        {/* Pratique — Amélioration continue */}
        <Route path="improvements" element={<Improvements />} />
        <Route path="improvements/:id" element={<ImprovementDetail />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
