import { routes as hubRoutes } from '../hub/src/routes';
import { routes as gridhubRoutes } from '../gridhub/src/routes';
import { routes as personaRoutes } from '../persona/src/routes';
import { routes as chronosRoutes } from '../chronos/src/routes';

export const allRoutes = (
  <>
    {hubRoutes}
    {gridhubRoutes}
    {personaRoutes}
    {chronosRoutes}
  </>
);
