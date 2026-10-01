// InventoryManagement.Web/wwwroot/js/date-range.js
//
// Date-range inputs on Air Datepicker (wwwroot/lib/air-datepicker, no dependencies), replacing
// the unmaintained daterangepicker + moment pair. The input shows "DD.MM.YYYY - DD.MM.YYYY";
// pages read it back with DateRange.parse() and send DateRange.iso() dates to the server.
//
//   DateRange.attach('#dateRange', {
//       onApply(start, end) {},   // both ends picked (Date objects, local midnight)
//       onClear() {}              // the Clear button
//   });
//   DateRange.set('#dateRange', start, end);   // show a range without firing onApply
//
// Requires air-datepicker.js on the page.

window.DateRange = (function () {
    'use strict';

    const SEPARATOR = ' - ';

    // Air Datepicker's own locale files are CommonJS modules; the page loads the UMD build,
    // so the locales live here. Both show dates as dd.MM.yyyy, the format used across the UI.
    const localeAz = {
        days: ['Bazar', 'Bazar ertəsi', 'Çərşənbə axşamı', 'Çərşənbə', 'Cümə axşamı', 'Cümə', 'Şənbə'],
        daysShort: ['B.', 'B.e.', 'Ç.a.', 'Ç.', 'C.a.', 'C.', 'Ş.'],
        daysMin: ['B', 'Be', 'Ça', 'Ç', 'Ca', 'C', 'Ş'],
        months: ['Yanvar', 'Fevral', 'Mart', 'Aprel', 'May', 'İyun', 'İyul', 'Avqust',
            'Sentyabr', 'Oktyabr', 'Noyabr', 'Dekabr'],
        monthsShort: ['Yan', 'Fev', 'Mar', 'Apr', 'May', 'İyn', 'İyl', 'Avq', 'Sen', 'Okt', 'Noy', 'Dek'],
        today: 'Bu gün',
        clear: 'Təmizlə',
        dateFormat: 'dd.MM.yyyy',
        timeFormat: 'HH:mm',
        firstDay: 1
    };

    const localeRu = {
        days: ['Воскресенье', 'Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота'],
        daysShort: ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'],
        daysMin: ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'],
        months: ['Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь', 'Июль', 'Август',
            'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'],
        monthsShort: ['Янв', 'Фев', 'Мар', 'Апр', 'Май', 'Июн', 'Июл', 'Авг', 'Сен', 'Окт', 'Ноя', 'Дек'],
        today: 'Сегодня',
        clear: 'Очистить',
        dateFormat: 'dd.MM.yyyy',
        timeFormat: 'HH:mm',
        firstDay: 1
    };

    const localeEn = {
        days: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'],
        daysShort: ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
        daysMin: ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'],
        months: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August',
            'September', 'October', 'November', 'December'],
        monthsShort: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
        today: 'Today',
        clear: 'Clear',
        dateFormat: 'dd.MM.yyyy',
        timeFormat: 'HH:mm',
        firstDay: 1
    };

    const pickers = new WeakMap();   // input -> AirDatepicker

    function element(target) {
        return typeof target === 'string' ? document.querySelector(target) : target;
    }

    function attach(target, options) {
        const input = element(target);
        if (!input || typeof AirDatepicker === 'undefined') return null;
        const opts = options || {};

        // Typing a range by hand is not supported; the calendar is the only way in.
        // (date-range-input keeps it looking like an active field, not a disabled one.)
        input.readOnly = true;
        input.classList.add('date-range-input');

        const picker = new AirDatepicker(input, {
            locale: ({ az: localeAz, ru: localeRu })[document.documentElement.lang] || localeEn,
            range: true,
            multipleDatesSeparator: SEPARATOR,
            autoClose: true,
            buttons: ['clear'],
            position: opts.position || 'bottom left',
            // Only the user's own picks arrive here: set()/clear() pass { silent: true }.
            onSelect: function ({ date }) {
                const dates = Array.isArray(date) ? date : (date ? [date] : []);
                if (dates.length === 2 && typeof opts.onApply === 'function') opts.onApply(dates[0], dates[1]);
                else if (dates.length === 0 && typeof opts.onClear === 'function') opts.onClear();
            }
        });

        pickers.set(input, picker);
        return picker;
    }

    // Air Datepicker runs onSelect in a later tick, so a flag around the call cannot suppress it;
    // { silent: true } is the library's own switch (the input is still updated). Without it,
    // restoring a range on page load fired onApply, which reloaded the page, which restored...
    function set(target, start, end) {
        const picker = pickers.get(element(target));
        if (picker && start && end) picker.selectDate([start, end], { silent: true });
    }

    function clear(target) {
        const picker = pickers.get(element(target));
        if (picker) picker.clear({ silent: true });
    }

    function parseDay(text) {
        const m = /^(\d{2})\.(\d{2})\.(\d{4})$/.exec(String(text || '').trim());
        return m ? new Date(+m[3], +m[2] - 1, +m[1]) : null;
    }

    /** "DD.MM.YYYY - DD.MM.YYYY" -> { start, end } (local dates), or null. */
    function parse(value) {
        const parts = String(value || '').split(SEPARATOR);
        if (parts.length !== 2) return null;
        const start = parseDay(parts[0]);
        const end = parseDay(parts[1]);
        return start && end ? { start: start, end: end } : null;
    }

    /** Date -> "YYYY-MM-DD" (local calendar date, as the server expects). */
    function iso(date) {
        const pad = n => String(n).padStart(2, '0');
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    /** "YYYY-MM-DD..." -> local Date, or null. */
    function fromIso(text) {
        const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(text || ''));
        return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
    }

    return { attach: attach, set: set, clear: clear, parse: parse, iso: iso, fromIso: fromIso };
})();
