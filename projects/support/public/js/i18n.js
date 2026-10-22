/* ============================================================
   ITS-KOM — Global i18n
   Usage: data-i18n="key"            → el.textContent
          data-i18n-html="key"       → el.innerHTML  (safe: static strings only)
          data-i18n-placeholder="key"→ el.placeholder
          data-i18n-title="key"      → el.title
   window.ITS_I18n.t(key)  — get translated string
   window.ITS_I18n.apply(lang) — apply lang to current page
   ============================================================ */
(function () {

    var T = {
        ru: {
            /* ── Navigation ── */
            'nav.home':             'Главная',
            'nav.profile':          'Профиль',
            'nav.logout.short':     'Выйти',
            'nav.logout.full':      'Выйти из системы',
            'nav.register':         'Регистрация',
            'nav.login':            'Войти',

            /* ── Home page ── */
            'home.subtitle':             'База знаний',
            'home.card.wiki.desc':       'Данные от серверов',
            'home.card.wiki.link':       'Перейти →',
            'home.card.events.title':    'События',
            'home.card.events.desc':     'Просмотр событий',
            'home.card.events.soon':     'Скоро',
            'home.hero.welcome':         'С возвращением,',
            'home.hero.title':           'База знаний ИТС-Ком',
            'home.hero.status':          'Все узлы в штатном режиме',
            'home.search.placeholder':   'Быстрый поиск серверов и документации (Ctrl + K)...',
            'home.search.empty':         'Серверов не найдено',
            'home.search.hint':          'Используйте ↑ ↓ для навигации, Enter для перехода',
            'home.widget.wiki.badge':    '25 организаций',
            'home.widget.wiki.status':   '● Активно',
            'home.widget.wiki.title':    'Wiki',
            'home.widget.wiki.desc':     'Технические параметры серверов, доступы и конфигурации.',
            'home.widget.wiki.btn':      'Перейти в базу',
            'home.widget.events.badge':  'Активно',
            'home.widget.events.status': 'Активно',
            'home.widget.events.title':  'События',
            'home.widget.events.desc':   'Журнал обновлений и релизов информационных баз 1С.',
            'home.widget.events.btn':    'Перейти к событиям',
            'home.widget.events.soon':   'Скоро',
            'home.quick.title':          'Быстрый доступ к серверам',
            'home.quick.subtitle':       'Часто используемые контуры и AnyDesk подключения в 1 клик',
            'home.quick.copy':           'Скопировать ID',
            'home.quick.copied':         'Скопировано!',
            'home.quick.open':           'Открыть →',
            'home.toast.copied':         'Скопировано в буфер: %s',

            /* ── Wiki sidebar ── */
            'wiki.sidebar.title':        'Оглавление',
            'wiki.sidebar.newBtn':       'Новая wiki-страница',
            'wiki.sidebar.empty':        'Нет организаций',
            'wiki.search.placeholder':   'Поиск...',
            'wiki.welcome':              'Выберите организацию из списка слева или нажмите <strong>+</strong> для создания новой.',

            /* ── Wiki content ── */
            'wiki.content.title':        'Содержимое страницы',
            'wiki.edit.btn':             'Редактировать',
            'wiki.view.btn':             'Просмотр',
            'wiki.more.btn':             'Ещё',
            'wiki.more.pdf':             '📄 Экспорт PDF',
            'wiki.more.txt':             '📝 Экспорт TXT',
            'wiki.more.delete':          '🗑 Удалить',
            'wiki.save.btn':             'Сохранить',
            'wiki.save.saving':          'Сохранение...',
            'wiki.last.modified':        'Последнее изменение:',
            'wiki.draft.loaded':         'Черновик загружен',
            'wiki.draft.saved':          'Черновик сохранён',

            /* ── Wiki toolbar ── */
            'toolbar.paragraph':         'Абзац',
            'toolbar.h1':                'Заголовок 1',
            'toolbar.h2':                'Заголовок 2',
            'toolbar.h3':                'Заголовок 3',

            /* ── Wiki modals ── */
            'wiki.modal.create.title':              'Новая организация',
            'wiki.modal.create.name.label':         'Название организации:',
            'wiki.modal.create.name.placeholder':   'Введите название',
            'wiki.modal.create.content.label':      'Содержимое (опционально):',
            'wiki.modal.create.content.placeholder':'Введите содержимое',
            'wiki.modal.create.btn':                'Создать',
            'wiki.modal.delete.title':              'Удалить организацию',
            'wiki.modal.delete.select':             'Выберите организацию для удаления:',
            'wiki.modal.delete.confirm':            'Вы уверены, что хотите удалить организацию',
            'wiki.modal.delete.none':               'Нет организаций для удаления.',
            'wiki.modal.delete.btn':                'Удалить',
            'wiki.modal.cancel.btn':                'Отмена',

            /* ── Wiki JS alerts ── */
            'wiki.alert.nameRequired':   'Пожалуйста, введите название организации',
            'wiki.alert.selectOrg':      'Выберите организацию для удаления',
            'wiki.confirm.delete':       'Вы уверены, что хотите удалить организацию "%s"?',
            'wiki.prompt.linkUrl':       'Введите URL ссылки:',

            /* ── Profile ── */
            'section.account':   'Информация об аккаунте',
            'field.login':       'Логин',
            'field.email':       'Email',
            'field.lang':        'Язык интерфейса',
            'field.created':     'Дата регистрации',
            'field.lastLogin':   'Последний вход',
            'btn.home':          'Главная',
            'btn.logout':        'Выйти из системы',
            'btn.save':          'Сохранить',
            'btn.cancel':        'Отменить',
            'email.none':        'Не указан',
            'email.editHint':    'Редактировать email',
            'email.saved':       'Email сохранён',
            'email.err.format':  'Неверный формат email',
            'email.err.save':    'Ошибка сохранения',
            'email.err.conn':    'Ошибка соединения',

            /* ── Login / Register ── */
            'login.title':           'Пользователь',
            'login.subtitle':        'База знаний',
            'login.username.label':  'Логин',
            'login.password':        'Пароль',
            'login.forgot':          'Восстановление пароля',
            'login.remember':        'Оставаться в системе',
            'login.btn':             'Войти',
            'login.quickHint':       'Локальный доступ:',
            'login.quickFill':       'Заполнить',
            'login.quickDone':       '✓ Заполнено!',
            'login.noAccount':       'Нет аккаунта?',
            'login.register':        'Зарегистрироваться',
            'login.username.ph':     'логин',
            'register.title':        'Регистрация нового пользователя',
            'register.login.label':  'Логин',
            'register.login.ph':     'Введите логин (минимум 3 символа)',
            'register.login.hint':   'Будет использоваться для входа в систему',
            'register.pass.label':   'Пароль',
            'register.pass.ph':      'Введите пароль (минимум 5 символов)',
            'register.pass.hint':    'Минимум 5 символов',
            'register.confirm.label':'Подтверждение пароля',
            'register.confirm.ph':   'Повторите пароль',
            'register.btn':          'Зарегистрироваться',
            'register.hasAccount':   'Уже есть аккаунт?',
            'register.login':        'Войти',
            'register.mismatch':     'Пароли не совпадают!'
        },

        kk: {
            /* ── Navigation ── */
            'nav.home':             'Басты бет',
            'nav.profile':          'Профиль',
            'nav.logout.short':     'Шығу',
            'nav.logout.full':      'Жүйеден шығу',
            'nav.register':         'Тіркелу',
            'nav.login':            'Кіру',

            /* ── Home page ── */
            'home.subtitle':             'Білім базасы',
            'home.card.wiki.desc':       'Серверлерден деректер',
            'home.card.wiki.link':       'Өту →',
            'home.card.events.title':    'Оқиғалар',
            'home.card.events.desc':     'Оқиғаларды қарау',
            'home.card.events.soon':     'Жақында',
            'home.hero.welcome':         'Қош келдіңіз,',
            'home.hero.title':           'ИТС-Ком білім базасы',
            'home.hero.status':          'Барлық тораптар қалыпты жұмыс істеуде',
            'home.search.placeholder':   'Серверлер мен құжаттаманы жылдам іздеу (Ctrl + K)...',
            'home.search.empty':         'Серверлер табылмады',
            'home.search.hint':          'Таңдау үшін ↑ ↓, өту үшін Enter басыңыз',
            'home.widget.wiki.badge':    '25 ұйым',
            'home.widget.wiki.status':   '● Белсенді',
            'home.widget.wiki.title':    'Wiki',
            'home.widget.wiki.desc':     'Серверлердің техникалық параметрлері, кіру рұқсаттары мен баптаулары.',
            'home.widget.wiki.btn':      'Базаға өту',
            'home.widget.events.badge':  'Белсенді',
            'home.widget.events.status': 'Белсенді',
            'home.widget.events.title':  'Оқиғалар',
            'home.widget.events.desc':   '1С ақпараттық базаларының жаңартулары мен шығарылымдары журналы.',
            'home.widget.events.btn':    'Оқиғаларға өту',
            'home.widget.events.soon':   'Жақында',
            'home.quick.title':          'Серверлерге жылдам кіру',
            'home.quick.subtitle':       'Жиі пайдаланылатын контурлар және 1 рет басу арқылы AnyDesk қосылымдары',
            'home.quick.copy':           'ID көшіру',
            'home.quick.copied':         'Көшірілді!',
            'home.quick.open':           'Ашу →',
            'home.toast.copied':         'Буферге көшірілді: %s',

            /* ── Wiki sidebar ── */
            'wiki.sidebar.title':        'Мазмұны',
            'wiki.sidebar.newBtn':       'Жаңа wiki-бет',
            'wiki.sidebar.empty':        'Ұйымдар жоқ',
            'wiki.search.placeholder':   'Іздеу...',
            'wiki.welcome':              'Сол жақтағы тізімнен ұйым таңдаңыз немесе жаңасын жасау үшін <strong>+</strong> басыңыз.',

            /* ── Wiki content ── */
            'wiki.content.title':        'Бет мазмұны',
            'wiki.edit.btn':             'Өңдеу',
            'wiki.view.btn':             'Қарау',
            'wiki.more.btn':             'Толығырақ',
            'wiki.more.pdf':             '📄 PDF экспорт',
            'wiki.more.txt':             '📝 TXT экспорт',
            'wiki.more.delete':          '🗑 Жою',
            'wiki.save.btn':             'Сақтау',
            'wiki.save.saving':          'Сақталуда...',
            'wiki.last.modified':        'Соңғы өзгерту:',
            'wiki.draft.loaded':         'Жобалама жүктелді',
            'wiki.draft.saved':          'Жобалама сақталды',

            /* ── Wiki toolbar ── */
            'toolbar.paragraph':         'Абзац',
            'toolbar.h1':                'Тақырып 1',
            'toolbar.h2':                'Тақырып 2',
            'toolbar.h3':                'Тақырып 3',

            /* ── Wiki modals ── */
            'wiki.modal.create.title':              'Жаңа ұйым',
            'wiki.modal.create.name.label':         'Ұйым атауы:',
            'wiki.modal.create.name.placeholder':   'Атауын енгізіңіз',
            'wiki.modal.create.content.label':      'Мазмұн (міндетті емес):',
            'wiki.modal.create.content.placeholder':'Мазмұнды енгізіңіз',
            'wiki.modal.create.btn':                'Жасау',
            'wiki.modal.delete.title':              'Ұйымды жою',
            'wiki.modal.delete.select':             'Жою үшін ұйымды таңдаңыз:',
            'wiki.modal.delete.confirm':            'Осы ұйымды жойғыңыз келетініне сенімдісіз бе',
            'wiki.modal.delete.none':               'Жоятын ұйым жоқ.',
            'wiki.modal.delete.btn':                'Жою',
            'wiki.modal.cancel.btn':                'Болдырмау',

            /* ── Wiki JS alerts ── */
            'wiki.alert.nameRequired':   'Ұйым атауын енгізіңіз',
            'wiki.alert.selectOrg':      'Жою үшін ұйымды таңдаңыз',
            'wiki.confirm.delete':       '"%s" ұйымын жойғыңыз келетініне сенімдісіз бе?',
            'wiki.prompt.linkUrl':       'Сілтеме URL мекенжайын енгізіңіз:',

            /* ── Profile ── */
            'section.account':   'Аккаунт туралы ақпарат',
            'field.login':       'Кіру аты',
            'field.email':       'Email',
            'field.lang':        'Интерфейс тілі',
            'field.created':     'Тіркелу күні',
            'field.lastLogin':   'Соңғы кіру',
            'btn.home':          'Басты бет',
            'btn.logout':        'Жүйеден шығу',
            'btn.save':          'Сақтау',
            'btn.cancel':        'Болдырмау',
            'email.none':        'Көрсетілмеген',
            'email.editHint':    'Email-ді өңдеу',
            'email.saved':       'Email сақталды',
            'email.err.format':  'Email форматы қате',
            'email.err.save':    'Сақтау қатесі',
            'email.err.conn':    'Қосылу қатесі',

            /* ── Login / Register ── */
            'login.title':           'Пайдаланушы',
            'login.subtitle':        'Білім базасы',
            'login.username.label':  'Кіру аты',
            'login.password':        'Құпия сөз',
            'login.forgot':          'Құпия сөзді қалпына келтіру',
            'login.remember':        'Жүйеде қалу',
            'login.btn':             'Кіру',
            'login.quickHint':       'Жергілікті кіру:',
            'login.quickFill':       'Толтыру',
            'login.quickDone':       '✓ Толтырылды!',
            'login.noAccount':       'Аккаунт жоқ па?',
            'login.register':        'Тіркелу',
            'login.username.ph':     'логин',
            'register.title':        'Жаңа пайдаланушыны тіркеу',
            'register.login.label':  'Кіру аты',
            'register.login.ph':     'Кіру атын енгізіңіз (кемінде 3 таңба)',
            'register.login.hint':   'Жүйеге кіру үшін пайдаланылады',
            'register.pass.label':   'Құпия сөз',
            'register.pass.ph':      'Құпия сөзді енгізіңіз (кемінде 5 таңба)',
            'register.pass.hint':    'Кемінде 5 таңба',
            'register.confirm.label':'Құпия сөзді растау',
            'register.confirm.ph':   'Құпия сөзді қайталаңыз',
            'register.btn':          'Тіркелу',
            'register.hasAccount':   'Аккаунт бар ма?',
            'register.login':        'Кіру',
            'register.mismatch':     'Құпия сөздер сәйкес келмейді!'
        },

        en: {
            /* ── Navigation ── */
            'nav.home':             'Home',
            'nav.profile':          'Profile',
            'nav.logout.short':     'Sign Out',
            'nav.logout.full':      'Sign Out',
            'nav.register':         'Register',
            'nav.login':            'Sign In',

            /* ── Home page ── */
            'home.subtitle':             'Knowledge Base',
            'home.card.wiki.desc':       'Data from servers',
            'home.card.wiki.link':       'Go →',
            'home.card.events.title':    'Events',
            'home.card.events.desc':     'View events',
            'home.card.events.soon':     'Soon',
            'home.hero.welcome':         'Welcome back,',
            'home.hero.title':           'ITS-Com Knowledge Base',
            'home.hero.status':          'All nodes operational',
            'home.search.placeholder':   'Quick search servers & documentation (Ctrl + K)...',
            'home.search.empty':         'No servers found',
            'home.search.hint':          'Use ↑ ↓ to navigate, Enter to select',
            'home.widget.wiki.badge':    '25 organizations',
            'home.widget.wiki.status':   '● Active',
            'home.widget.wiki.title':    'Wiki',
            'home.widget.wiki.desc':     'Server technical parameters, access credentials and configurations.',
            'home.widget.wiki.btn':      'Open Knowledge Base →',
            'home.widget.events.badge':  'Active',
            'home.widget.events.status': 'Active',
            'home.widget.events.title':  'Events',
            'home.widget.events.desc':   'Log of updates and releases of 1C information databases.',
            'home.widget.events.btn':    'Go to events',
            'home.widget.events.soon':   'Soon',
            'home.quick.title':          'Quick Access Servers',
            'home.quick.subtitle':       'Frequently used environments and 1-click AnyDesk connections',
            'home.quick.copy':           'Copy ID',
            'home.quick.copied':         'Copied!',
            'home.quick.open':           'Open →',
            'home.toast.copied':         'Copied to clipboard: %s',

            /* ── Wiki sidebar ── */
            'wiki.sidebar.title':        'Contents',
            'wiki.sidebar.newBtn':       'New wiki page',
            'wiki.sidebar.empty':        'No organizations',
            'wiki.search.placeholder':   'Search...',
            'wiki.welcome':              'Select an organization from the list on the left or click <strong>+</strong> to create a new one.',

            /* ── Wiki content ── */
            'wiki.content.title':        'Page content',
            'wiki.edit.btn':             'Edit',
            'wiki.view.btn':             'View',
            'wiki.more.btn':             'More',
            'wiki.more.pdf':             '📄 Export PDF',
            'wiki.more.txt':             '📝 Export TXT',
            'wiki.more.delete':          '🗑 Delete',
            'wiki.save.btn':             'Save',
            'wiki.save.saving':          'Saving...',
            'wiki.last.modified':        'Last modified:',
            'wiki.draft.loaded':         'Draft loaded',
            'wiki.draft.saved':          'Draft saved',

            /* ── Wiki toolbar ── */
            'toolbar.paragraph':         'Paragraph',
            'toolbar.h1':                'Heading 1',
            'toolbar.h2':                'Heading 2',
            'toolbar.h3':                'Heading 3',

            /* ── Wiki modals ── */
            'wiki.modal.create.title':              'New organization',
            'wiki.modal.create.name.label':         'Organization name:',
            'wiki.modal.create.name.placeholder':   'Enter name',
            'wiki.modal.create.content.label':      'Content (optional):',
            'wiki.modal.create.content.placeholder':'Enter content',
            'wiki.modal.create.btn':                'Create',
            'wiki.modal.delete.title':              'Delete organization',
            'wiki.modal.delete.select':             'Select an organization to delete:',
            'wiki.modal.delete.confirm':            'Are you sure you want to delete the organization',
            'wiki.modal.delete.none':               'No organizations to delete.',
            'wiki.modal.delete.btn':                'Delete',
            'wiki.modal.cancel.btn':                'Cancel',

            /* ── Wiki JS alerts ── */
            'wiki.alert.nameRequired':   'Please enter an organization name',
            'wiki.alert.selectOrg':      'Select an organization to delete',
            'wiki.confirm.delete':       'Are you sure you want to delete the organization "%s"?',
            'wiki.prompt.linkUrl':       'Enter link URL:',

            /* ── Profile ── */
            'section.account':   'Account Information',
            'field.login':       'Login',
            'field.email':       'Email',
            'field.lang':        'Interface Language',
            'field.created':     'Registration Date',
            'field.lastLogin':   'Last Login',
            'btn.home':          'Home',
            'btn.logout':        'Sign Out',
            'btn.save':          'Save',
            'btn.cancel':        'Cancel',
            'email.none':        'Not specified',
            'email.editHint':    'Edit email',
            'email.saved':       'Email saved',
            'email.err.format':  'Invalid email format',
            'email.err.save':    'Save error',
            'email.err.conn':    'Connection error',

            /* ── Login / Register ── */
            'login.title':           'User',
            'login.subtitle':        'Knowledge Base',
            'login.username.label':  'Username',
            'login.password':        'Password',
            'login.forgot':          'Forgot password',
            'login.remember':        'Stay logged in',
            'login.btn':             'Sign In',
            'login.quickHint':       'Local access:',
            'login.quickFill':       'Autofill',
            'login.quickDone':       '✓ Filled!',
            'login.noAccount':       'No account?',
            'login.register':        'Register',
            'login.username.ph':     'username',
            'register.title':        'Register new user',
            'register.login.label':  'Login',
            'register.login.ph':     'Enter login (min 3 characters)',
            'register.login.hint':   'Will be used to sign in',
            'register.pass.label':   'Password',
            'register.pass.ph':      'Enter password (min 5 characters)',
            'register.pass.hint':    'Minimum 5 characters',
            'register.confirm.label':'Confirm password',
            'register.confirm.ph':   'Repeat password',
            'register.btn':          'Register',
            'register.hasAccount':   'Already have an account?',
            'register.login':        'Sign In',
            'register.mismatch':     'Passwords do not match!'
        }
    };

    function t(key) {
        var lang = localStorage.getItem('ui_lang') || 'ru';
        var dict = T[lang] || T['ru'];
        return dict[key] !== undefined ? dict[key] : (T['ru'][key] || key);
    }

    function tf(key, val) {
        return t(key).replace('%s', val || '');
    }

    function applyLang(lang) {
        var dict = T[lang] || T['ru'];

        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            if (dict[key] !== undefined) el.textContent = dict[key];
        });

        document.querySelectorAll('[data-i18n-html]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-html');
            if (dict[key] !== undefined) el.innerHTML = dict[key];
        });

        document.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-placeholder');
            if (dict[key] !== undefined) el.placeholder = dict[key];
        });

        document.querySelectorAll('[data-i18n-title]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-title');
            if (dict[key] !== undefined) el.title = dict[key];
        });

        document.documentElement.lang = lang === 'en' ? 'en' : lang === 'kk' ? 'kk' : 'ru';
    }

    /* ── Theme management ─────────────────────────────── */
    function getThemeKey() {
        var meta = document.querySelector('meta[name="app-user"]');
        var u = meta && meta.getAttribute('content');
        return u ? 'ui_theme_' + u : 'ui_theme';
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem(getThemeKey(), theme);
    }

    function initTheme() {
        var key = getThemeKey();
        var saved = localStorage.getItem(key) || 'light';
        document.documentElement.setAttribute('data-theme', saved);
        var btn = document.getElementById('themeToggle');
        if (!btn) return;
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            var current = document.documentElement.getAttribute('data-theme') || 'light';
            applyTheme(current === 'dark' ? 'light' : 'dark');
        });
    }

    function syncTopbarAvatar() {
        var meta = document.querySelector('meta[name="app-user"]');
        if (!meta) return;
        var username = meta.getAttribute('content');
        if (!username) return;
        var avatarData = localStorage.getItem('profile_avatar_' + username);
        if (!avatarData) return;
        var avatarEl = document.querySelector('.home-account-avatar');
        if (!avatarEl) return;
        avatarEl.innerHTML = '';
        var img = document.createElement('img');
        img.src = avatarData;
        img.alt = '';
        img.style.cssText = 'width:100%;height:100%;object-fit:cover;border-radius:50%;display:block;';
        avatarEl.appendChild(img);
    }

    window.ITS_I18n = { t: t, tf: tf, apply: applyLang, syncTopbar: syncTopbarAvatar, applyTheme: applyTheme };

    /* Auto-apply on every page load */
    function boot() {
        applyLang(localStorage.getItem('ui_lang') || 'ru');
        syncTopbarAvatar();
        initTheme();
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

}());
