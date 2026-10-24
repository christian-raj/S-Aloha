// Le papier est blanc : un rapport imprimé en thème sombre (le défaut) sort en
// texte clair sur fond blanc dès que le navigateur n'imprime pas les fonds (son
// réglage par défaut). On imprime donc toujours en thème clair, puis on rend à
// l'écran le thème choisi — export PDF du reporting comme Ctrl+P.
export function printInLightTheme(root = document.documentElement, target = window) {
  let restore = false
  const before = () => { restore = root.classList.contains('dark'); root.classList.remove('dark') }
  const after = () => { if (restore) root.classList.add('dark'); restore = false }
  target.addEventListener('beforeprint', before)
  target.addEventListener('afterprint', after)
  return () => { target.removeEventListener('beforeprint', before); target.removeEventListener('afterprint', after) }
}
