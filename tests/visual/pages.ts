export interface VisualPage {
  site: 'community' | 'codegarden';
  path: string;
  variant?: string;
  addClasses?: { selector: string; classes: string[] }[];
}

const community = [
  '/',
  '/learn/',
  '/learn/newcomers-hub/',
  '/contribute/',
  '/contribute/mvp-program/',
  '/events/',
  '/events/meetup-hub/',
  '/showcase/',
  '/connect/',
  '/blog/',
  '/docs/',
  '/code-of-conduct/',
  '/community-keynote-2026/',
  '/search/',
  '/visual-regression-404/',
  '/contribute/become-a-speaker/',
  '/showcase/uprofiles/',
  '/showcase/community-blogs/',
  '/links/community-newsletter-from-lurker-to-leader/',
  '/events/meetup-hub/join-a-meetup/',
];

const codegarden = [
  '/codegarden/',
  '/contact/',
  '/tickets/',
  '/awards/',
  '/speakers/',
  '/program/',
  '/program/social-activities/',
  '/program/schedule-overview/',
  '/program/tuesday-workshops-and-course/',
  '/convince-your-boss/',
  '/diversity-and-inclusion/',
  '/diversity-and-inclusion/student-tickets/',
  '/practical-information/',
  '/practical-information/sustainability/',
];

export const pages: VisualPage[] = [
  ...community.map(path => ({ site: 'community' as const, path })),
  ...codegarden.map(path => ({ site: 'codegarden' as const, path })),
  {
    site: 'community',
    path: '/',
    variant: 'forced-dark',
    addClasses: [
      {
        selector: '.dc-blog-showcase-block, .dc-boxes, .dc-community-blogs, .dc-upcoming-events, .dc-text-block, .dc-image-and-text-block, .dc-slider-block',
        classes: ['has-bg', 'bg-dark'],
      },
    ],
  },
  {
    site: 'community',
    path: '/contribute/mvp-program/',
    variant: 'forced-dark',
    addClasses: [{ selector: '.dc-slider-block, .dc-blog-showcase-block, .dc-text-block', classes: ['has-bg', 'bg-dark'] }],
  },
];

export const slug = ({ site, path, variant }: VisualPage) =>
  `${site}-${path.replace(/^\/|\/$/g, '').replace(/\//g, '_') || 'home'}${variant ? `-${variant}` : ''}`;

export const maskSelectors = [
  'dc-sessionize-program',
  'dc-sessionize-speakers',
  'iframe',
  'video',
  '[data-visual-mask]',
];
