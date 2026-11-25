import './styles/fonts.css';
import { createRoot } from 'react-dom/client';
import './styles/globals.scss';
import './styles/stitch.scss';
import './styles/theme.scss';
import './styles/responsive.scss';
import './styles/dashboard.scss';
import './styles/workspace.scss';
import './styles/hub-home.scss';
import App from './App.jsx';
import { portalName } from './utils/portals';
document.title = portalName;
// A portal passes its own routes. The landing page passes its own root instead, because it is not a portal.
export function mountPortal(routes, Root = App) {
  document.title = portalName;
  createRoot(document.getElementById('root')).render(<Root routes={routes} />);
}
