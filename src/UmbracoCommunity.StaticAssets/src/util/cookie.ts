export default class Cookie {
    static getCookie(key: string, nullIfEmpty = false) {
        var name = key + '=';
        var ca = document.cookie.split(';');
        for (var i = 0; i < ca.length; i++) {
            var c = ca[i];
            while (c.charAt(0) === ' ') {
                c = c.substring(1);
            }
            if (c.indexOf(name) === 0) {
                return c.substring(name.length, c.length);
            }
        }

        return nullIfEmpty ? null : '';
    }

    static setCookie(key: string, value: string, expires: number) {
        var d = new Date();
        d.setTime(d.getTime() + expires * 60 * 60 * 1000 * 24);

        document.cookie = key + '=' + value + '; expires=' + d.toUTCString() + ';path=/';
    }
}
