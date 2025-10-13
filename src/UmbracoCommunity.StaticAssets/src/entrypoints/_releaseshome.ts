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
