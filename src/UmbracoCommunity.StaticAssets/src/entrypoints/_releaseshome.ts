import '../css/pages/releaseshome.css';

// Handle compare form auto-submit
const compareForm = document.getElementById('compareForm') as HTMLFormElement | null;
if (compareForm) {
  const fromVersion = document.getElementById('fromVersion') as HTMLSelectElement | null;
  const toVersion = document.getElementById('toVersion') as HTMLSelectElement | null;
  const includePreReleases = document.getElementById('includePreReleases') as HTMLInputElement | null;

  if (fromVersion && toVersion && includePreReleases) {
    fromVersion.addEventListener('change', () => compareForm.submit());
    toVersion.addEventListener('change', () => compareForm.submit());
    includePreReleases.addEventListener('change', () => compareForm.submit());
  }
}

// Handle sticky version bar on scroll
const stickyVersionBar = document.getElementById('stickyVersionBar');
if (stickyVersionBar) {
  let lastScrollY = window.scrollY;
  let ticking = false;

  const updateStickyBar = () => {
    const scrollY = window.scrollY;

    // Show sticky bar after scrolling down 200px
    if (scrollY > 200) {
      stickyVersionBar.classList.add('visible');
    } else {
      stickyVersionBar.classList.remove('visible');
    }

    lastScrollY = scrollY;
    ticking = false;
  };

  const onScroll = () => {
    if (!ticking) {
      window.requestAnimationFrame(updateStickyBar);
      ticking = true;
    }
  };

  window.addEventListener('scroll', onScroll, { passive: true });
}

// Handle nav burger menu toggle
const navBurger = document.getElementById('navBurger');
const navMobileMenu = document.getElementById('navMobileMenu');

if (navBurger && navMobileMenu) {
    navBurger.addEventListener('click', () => {
        navBurger.classList.toggle('active');
        navMobileMenu.classList.toggle('active');
    });
}
