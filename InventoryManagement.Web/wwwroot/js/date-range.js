// Date-range inputs on Air Datepicker, read with DateRange.parse() and sent as DateRange.iso() dates

window.DateRange = (function () {
    'use strict';

    const SEPARATOR = ' - ';

    // The library's locale files are CommonJS and the page loads the UMD build, so locales live here
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

    const pickers = new WeakMap();

    function element(target) {
        return typeof target === 'string' ? document.querySelector(target) : target;
    }

    function attach(target, options) {
        const input = element(target);
        if (!input || typeof AirDatepicker === 'undefined') return null;
        const opts = options || {};

        // The calendar is the only way to enter a range, the class keeps the field looking editable
        if (!opts.inline) {
            input.readOnly = true;
            input.classList.add('date-range-input');
        }

        const picker = new AirDatepicker(input, {
            locale: ({ az: localeAz, ru: localeRu })[document.documentElement.lang] || localeEn,
            range: true,
            multipleDatesSeparator: SEPARATOR,
            inline: !!opts.inline,
            autoClose: !opts.inline,
            buttons: opts.inline ? false : ['clear'],
            position: opts.position || 'bottom left',
            // Only real picks arrive here because set() and clear() are silent
            onSelect: function ({ date }) {
                const dates = Array.isArray(date) ? date : (date ? [date] : []);
                if (dates.length === 2 && typeof opts.onApply === 'function') opts.onApply(dates[0], dates[1]);
                else if (dates.length === 0 && typeof opts.onClear === 'function') opts.onClear();
            }
        });

        pickers.set(input, picker);
        return picker;
    }

    // onSelect fires in a later tick, so a non-silent restore on load would reload the page in a loop
    function set(target, start, end) {
        const picker = pickers.get(element(target));
        if (picker && start && end) picker.selectDate([start, end], { silent: true });
    }

    function clear(target) {
        const picker = pickers.get(element(target));
        if (picker) picker.clear({ silent: true });
    }

    /** The picked dates, two for a complete range. */
    function selected(target) {
        const picker = pickers.get(element(target));
        return picker ? picker.selectedDates.slice() : [];
    }

    function parseDay(text) {
        const m = /^(\d{2})\.(\d{2})\.(\d{4})$/.exec(String(text || '').trim());
        return m ? new Date(+m[3], +m[2] - 1, +m[1]) : null;
    }

    /** Parses the input text into local start and end dates, or returns null. */
    function parse(value) {
        const parts = String(value || '').split(SEPARATOR);
        if (parts.length !== 2) return null;
        const start = parseDay(parts[0]);
        const end = parseDay(parts[1]);
        return start && end ? { start: start, end: end } : null;
    }

    /** Formats the local calendar day as YYYY-MM-DD, which the server expects. */
    function iso(date) {
        const pad = n => String(n).padStart(2, '0');
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    /** Reads a YYYY-MM-DD prefix as a local date, or returns null. */
    function fromIso(text) {
        const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(text || ''));
        return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
    }

    return { attach: attach, set: set, clear: clear, selected: selected, parse: parse, iso: iso, fromIso: fromIso };
})();
