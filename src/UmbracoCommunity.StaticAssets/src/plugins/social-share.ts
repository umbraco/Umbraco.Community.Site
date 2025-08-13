export class SocialShare {
    _isOpened: boolean;
    _container: HTMLDivElement;
    _itemsContainer: HTMLDivElement;
    _toggleButton: HTMLDivElement;

    constructor(socialShareContainer: HTMLDivElement) {
        this._isOpened = false;
        this._container = socialShareContainer;
        this._itemsContainer = this._container.querySelector('.social-share__items')!;
        this._toggleButton = this._container.querySelector('.social-share__toggle')!;
    }

    _setVisible(element: HTMLElement, isVisible: boolean) {
        if (isVisible) element.classList.remove('invisible');
        else element.classList.add('invisible');
    }

    _updateButtonIcon() {
        const closeIcon = 'opened';
        const iconElement = this._toggleButton.querySelector('svg')!;

        if (this._isOpened) {
            iconElement.classList.add(closeIcon);
        }
        else {
            iconElement.classList.remove(closeIcon);
        }
    }

    _update() {
        this._setVisible(this._itemsContainer, this._isOpened);
        this._updateButtonIcon();
    }

    init() {
        this._update();
        this._setVisible(this._container, true);

        this._toggleButton.onclick = () => {
            this._isOpened = !this._isOpened;
            this._update();
        };
    }
}