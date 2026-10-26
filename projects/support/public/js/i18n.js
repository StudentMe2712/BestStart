/* ============================================================
   ITS-KOM — Global i18n
   Usage: data-i18n="key"            → el.textContent (or span if svg/counter)
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
            'nav.wiki':             'Wiki',
            'nav.adminUsers':       'Пользователи',
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
            'home.hero.wiki.title':      'Wiki — данные серверов',
            'home.hero.wiki.p1':         'Защищённое хранилище технической информации по организациям: настройки серверов, данные удалённого доступа и конфигурации клиентов ИТС-Ком.',
            'home.hero.wiki.p2':         'Можно добавлять заметки и прикреплять необходимые файлы — Excel-таблицы, схемы, PDF-инструкции и другие документы.',
            'home.hero.wiki.tag.orgs':   'организаций',
            'home.hero.wiki.tag.files':  'Вложения Excel / PDF / Схемы',
            'home.hero.wiki.tag.export': 'Экспорт PDF и TXT',
            'home.hero.wiki.btn':        'Перейти в базу знаний',
            'home.pinned.title':         'Мои закрепленные организации',
            'home.pinned.badge':         'Быстрый доступ',
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
            'home.widget.events.desc':   'Иерархический журнал и реестр информационных баз 1С.',
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
            'wiki.sidebar.pinned':       'ЗАКРЕПЛЕННЫЕ',
            'wiki.sidebar.all':          'ВСЕ ОРГАНИЗАЦИИ',
            'wiki.sidebar.newBtn':       'Новая wiki-страница',
            'wiki.sidebar.empty':        'Нет организаций',
            'wiki.sidebar.noResults':    'Ничего не найдено',
            'wiki.search.placeholder':   'Поиск...',
            'wiki.welcome':              'Выберите организацию из списка слева или нажмите <strong>+</strong> для создания новой.',

            /* ── Wiki content ── */
            'wiki.content.title':        'Содержимое страницы',
            'wiki.edit.btn':             'Редактировать',
            'wiki.cancel.btn':           'Отмена',
            'wiki.save.btn':             'Сохранить',
            'wiki.view.btn':             'Просмотр',
            'wiki.more.btn':             'Ещё',
            'wiki.more.pdf':             'Экспорт PDF',
            'wiki.more.txt':             'Экспорт TXT',
            'wiki.more.delete':          'Удалить',
            'wiki.save.saving':          'Сохранение...',
            'wiki.last.modified':        'Последнее изменение:',
            'wiki.draft.loaded':         'Черновик загружен',
            'wiki.draft.saved':          'Черновик сохранён',

            /* ── Wiki tabs & attachments ── */
            'wiki.tab.data':             'Данные',
            'wiki.tab.files':            'Файлы',
            'wiki.files.title':          'Присоединенные файлы',
            'wiki.files.add':            'Добавить файл',
            'wiki.files.attach':         'Прикрепить файл',
            'wiki.files.empty.title':    'К этой организации еще не прикреплены файлы',
            'wiki.files.empty.desc':     'Поддерживаются форматы Excel (.xlsx, .xls), Word (.docx, .doc), PDF, изображения (.png, .jpg, .webp), XML, TXT, ZIP и RDP',
            'wiki.files.download':       'Скачать файл',
            'wiki.files.delete':         'Удалить файл',
            'wiki.files.preview':        'Нажмите для предпросмотра',

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

            /* ── Confirmation Modal ── */
            'confirm.delete.title':      'Удалить вложение?',
            'confirm.delete.desc':       'Вы действительно хотите удалить этот объект?',
            'confirm.delete.warning':    'Это действие необратимо. Восстановить данные будет невозможно.',
            'confirm.delete.btn':        'Удалить',
            'confirm.cancel.btn':        'Отмена',

            /* ── Toasts ── */
            'toast.pinned':              'Организация закреплена',
            'toast.unpinned':            'Организация откреплена',
            'toast.fileUploaded':        'Файл прикреплен',
            'toast.fileDeleted':         'Файл удален',

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

            /* ── Profile & Password Change (Sprint 12) ── */
            'profile.mustChange.title': 'Требуется сменить пароль',
            'profile.mustChange.desc': 'Ваш пароль был сброшен администратором. Для продолжения работы в системе установите новый постоянный пароль.',
            'profile.section.password': 'Смена пароля',
            'profile.password.current': 'Текущий пароль',
            'profile.password.new': 'Новый пароль',
            'profile.password.confirm': 'Подтверждение нового пароля',
            'profile.password.hint': 'Минимум 8 символов',
            'profile.password.btn': 'Обновить пароль',
            'profile.password.success': 'Пароль успешно обновлён',
            'profile.password.err.current': 'Введите текущий пароль',
            'profile.password.err.length': 'Пароль должен содержать минимум 8 символов',
            'profile.password.err.match': 'Пароли не совпадают',
            'profile.password.err.same': 'Новый пароль должен отличаться от текущего',
            'profile.password.err.save': 'Ошибка смены пароля',

            /* ── Admin Users Management (Sprint 12) ── */
            'admin.users.title': 'Управление пользователями',
            'admin.users.subtitle': 'Административный сброс паролей и безопасность учетных записей',
            'admin.users.count': 'пользователей',
            'admin.users.btn.create': 'Добавить пользователя',
            'admin.users.col.id': '#',
            'admin.users.col.user': 'Пользователь',
            'admin.users.col.role': 'Роль',
            'admin.users.col.status': 'Статус',
            'admin.users.col.created': 'Создан',
            'admin.users.col.lastLogin': 'Последний вход',
            'admin.users.col.actions': 'Действия',
            'admin.users.role.admin': 'Администратор',
            'admin.users.role.engineer': 'Инженер',
            'admin.users.status.active': 'Активен',
            'admin.users.status.mustChange': 'Смена пароля',
            'admin.users.btn.reset': 'Сбросить пароль',
            'admin.users.btn.edit': 'Редактировать',
            'admin.users.btn.delete': 'Удалить',
            'admin.users.btn.deleteSelfForbidden': 'Удаление собственной учётной записи запрещено',
            'admin.users.protected': 'Защищено',
            'admin.users.empty': 'Пользователи не найдены',
            'admin.users.modal.create.title': 'Добавить пользователя',
            'admin.users.modal.create.username': 'Логин',
            'admin.users.modal.create.usernamePh': 'Введите логин',
            'admin.users.modal.create.email': 'Электронная почта (необязательно)',
            'admin.users.modal.create.emailPh': 'user@its-kom.kz',
            'admin.users.modal.create.roleNote': 'Роль: Инженер (назначается автоматически)',
            'admin.users.modal.create.submit': 'Создать пользователя',
            'admin.users.modal.create.success': 'Пользователь успешно создан',
            'admin.users.modal.edit.title': 'Редактирование пользователя',
            'admin.users.modal.edit.roleLocked': 'Роль:',
            'admin.users.modal.edit.roleNote': '(изменение роли запрещено)',
            'admin.users.modal.edit.save': 'Сохранить',
            'admin.users.modal.edit.success': 'Данные пользователя успешно обновлены',
            'admin.users.modal.delete.title': 'Удалить пользователя?',
            'admin.users.modal.delete.desc': 'Вы действительно хотите удалить учётную запись пользователя:',
            'admin.users.modal.delete.warn': 'Это действие необратимо. Доступ пользователя и его активные сессии будут немедленно прекращены.',
            'admin.users.modal.delete.btn': 'Удалить',
            'admin.users.modal.delete.success': 'Пользователь успешно удален',
            'admin.users.modal.confirm.title': 'Сброс пароля пользователя',
            'admin.users.modal.confirm.warn': 'Будет сгенерирован новый постоянный пароль, а активные сессии пользователя будут немедленно завершены.',
            'admin.users.modal.confirm.btn': 'Сгенерировать новый пароль',
            'admin.users.modal.result.title': 'Новый пароль установлен',
            'admin.users.modal.result.createdTitle': 'Пользователь создан',
            'admin.users.modal.result.copy': 'Скопировать',
            'admin.users.modal.result.copied': 'Скопировано!',
            'admin.users.modal.result.warn': '⚠️ Сохраните или передайте пароль сотруднику. Он отображается один раз и не будет доступен повторно.',
            'admin.users.modal.result.close': 'Закрыть',

            /* ── Login / Reset Password / Register ── */
            'login.title':              'База знаний',
            'login.subtitle':           'База знаний',
            'login.username.label':     'Логин',
            'login.username.ph':        'логин',
            'login.password':           'Пароль',
            'login.password.ph':        'пароль',
            'login.forgot':             'Восстановление пароля',
            'login.remember':           'Оставаться в системе',
            'login.btn':                'Войти',
            'login.reset.step1':        'Шаг 1 из 2: Проверка учетной записи',
            'login.reset.step2':        'Шаг 2 из 2: Новый пароль',
            'login.reset.verify':       'Проверить',
            'login.reset.newPass':      'Новый пароль',
            'login.reset.confirmPass':  'Повторите пароль',
            'login.reset.savePass':     'Сохранить пароль',
            'login.reset.back':         'Вернуться ко входу',
            'login.reset.user':         'Пользователь:',
            'login.quickHint':          'Локальный доступ:',
            'login.quickFill':          'Заполнить',
            'login.quickDone':          '✓ Заполнено!',
            'login.noAccount':          'Нет аккаунта?',
            'login.register':           'Зарегистрироваться',
            'register.title':           'Регистрация нового пользователя',
            'register.login.label':     'Логин',
            'register.login.ph':        'Введите логин (минимум 3 символа)',
            'register.login.hint':      'Будет использоваться для входа в систему',
            'register.pass.label':      'Пароль',
            'register.pass.ph':         'Введите пароль (минимум 5 символов)',
            'register.pass.hint':       'Минимум 5 символов',
            'register.confirm.label':   'Подтверждение пароля',
            'register.confirm.ph':      'Повторите пароль',
            'register.btn':             'Зарегистрироваться',
            'register.hasAccount':      'Уже есть аккаунт?',
            'register.login':           'Войти',
            'register.mismatch':        'Пароли не совпадают!'
        },

        kk: {
            /* ── Navigation ── */
            'nav.home':             'Басты бет',
            'nav.wiki':             'Wiki',
            'nav.adminUsers':       'Пайдаланушылар',
            'nav.profile':          'Профиль',
            'nav.logout.short':     'Шығу',
            'nav.logout.full':      'Жүйеден шығу',
            'nav.register':         'Тіркелу',
            'nav.login':            'Кіру',

            /* ── Home page ── */
            'home.subtitle':             'Білім қоры',
            'home.card.wiki.desc':       'Серверлерден деректер',
            'home.card.wiki.link':       'Өту →',
            'home.card.events.title':    'Оқиғалар',
            'home.card.events.desc':     'Оқиғаларды қарау',
            'home.card.events.soon':     'Жақында',
            'home.hero.welcome':         'Қайта оралуыңызбен,',
            'home.hero.title':           'ИТС-Ком Білім қоры',
            'home.hero.status':          'Барлық тораптар штаттық режимде',
            'home.hero.wiki.title':      'Wiki — Серверлік білім қоры',
            'home.hero.wiki.p1':         'Ұйымдар бойынша техникалық ақпараттың қорғалған қоймасы: серверлер баптаулары, қашықтан кіру деректері және ИТС-Ком клиенттерінің конфигурациялары.',
            'home.hero.wiki.p2':         'Жазбалар қосуға және қажетті файлдарды — Excel кестелерін, схемаларды, PDF нұсқаулықтарды және басқа құжаттарды тіркеуге болады.',
            'home.hero.wiki.tag.orgs':   'ұйым',
            'home.hero.wiki.tag.files':  'Excel / PDF / Схема тіркемелері',
            'home.hero.wiki.tag.export': 'PDF және TXT экспорттау',
            'home.hero.wiki.btn':        'Білім қорына өту →',
            'home.pinned.title':         'Менің бекітілген ұйымдарым',
            'home.pinned.badge':         'Жылдам қолжетімділік',
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
            'wiki.sidebar.pinned':       'БЕКІТІЛГЕНДЕР',
            'wiki.sidebar.all':          'БАРЛЫҚ ҰЙЫМДАР',
            'wiki.sidebar.newBtn':       'Жаңа wiki-бет',
            'wiki.sidebar.empty':        'Ұйымдар жоқ',
            'wiki.sidebar.noResults':    'Ештеңе табылмады',
            'wiki.search.placeholder':   'Іздеу...',
            'wiki.welcome':              'Сол жақтағы тізімнен ұйым таңдаңыз немесе жаңасын жасау үшін <strong>+</strong> басыңыз.',

            /* ── Wiki content ── */
            'wiki.content.title':        'Бет мазмұны',
            'wiki.edit.btn':             'Өңдеу',
            'wiki.cancel.btn':           'Болдырмау',
            'wiki.save.btn':             'Сақтау',
            'wiki.view.btn':             'Қарау',
            'wiki.more.btn':             'Тағы',
            'wiki.more.pdf':             'PDF экспорттау',
            'wiki.more.txt':             'TXT экспорттау',
            'wiki.more.delete':          'Жою',
            'wiki.save.saving':          'Сақталуда...',
            'wiki.last.modified':        'Соңғы өзгеріс:',
            'wiki.draft.loaded':         'Жобалама жүктелді',
            'wiki.draft.saved':          'Жобалама сақталды',

            /* ── Wiki tabs & attachments ── */
            'wiki.tab.data':             'Деректер',
            'wiki.tab.files':            'Файлдар',
            'wiki.files.title':          'Тіркелген файлдар',
            'wiki.files.add':            '+ Файл қосу',
            'wiki.files.attach':         'Файл тіркеу',
            'wiki.files.empty.title':    'Бұл ұйымға әлі файлдар тіркелмеген',
            'wiki.files.empty.desc':     'Excel (.xlsx, .xls), Word (.docx, .doc), PDF, кескіндер (.png, .jpg, .webp), XML, TXT, ZIP және RDP форматтары қолдау табады',
            'wiki.files.download':       'Жүктеп алу',
            'wiki.files.delete':         'Жою',
            'wiki.files.preview':        'Ашу',

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
            'wiki.modal.create.btn':                'Құру',
            'wiki.modal.delete.title':              'Ұйымды жою',
            'wiki.modal.delete.select':             'Жою үшін ұйымды таңдаңыз:',
            'wiki.modal.delete.confirm':            'Осы ұйымды жойғыңыз келетініне сенімдісіз бе',
            'wiki.modal.delete.none':               'Жоятын ұйым жоқ.',
            'wiki.modal.delete.btn':                'Жою',
            'wiki.modal.cancel.btn':                'Болдырмау',

            /* ── Confirmation Modal ── */
            'confirm.delete.title':      'Жоюды растау',
            'confirm.delete.desc':       'Сіз шынымен осы нысанды жойғыңыз келеді ме?',
            'confirm.delete.warning':    'Бұл әрекетті қайтару мүмкін емес. Деректерді қалпына келтіру мүмкін болмайды.',
            'confirm.delete.btn':        'Жою',
            'confirm.cancel.btn':        'Болдырмау',

            /* ── Toasts ── */
            'toast.pinned':              'Ұйым бекітілді',
            'toast.unpinned':            'Ұйым босатылды',
            'toast.fileUploaded':        'Файл тіркелді',
            'toast.fileDeleted':         'Файл жойылды',

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

            /* ── Profile & Password Change (Sprint 12) ── */
            'profile.mustChange.title': 'Құпия сөзді өзгерту қажет',
            'profile.mustChange.desc': 'Сіздің құпия сөзіңізді әкімші қалпына келтірді. Жұмысты жалғастыру үшін жаңа тұрақты құпия сөзді орнатыңыз.',
            'profile.section.password': 'Құпия сөзді өзгерту',
            'profile.password.current': 'Ағымдағы құпия сөз',
            'profile.password.new': 'Жаңа құпия сөз',
            'profile.password.confirm': 'Жаңа құпия сөзді растау',
            'profile.password.hint': 'Кемінде 8 таңба',
            'profile.password.btn': 'Құпия сөзді жаңарту',
            'profile.password.success': 'Құпия сөз сәтті жаңартылды',
            'profile.password.err.current': 'Ағымдағы құпия сөзді енгізіңіз',
            'profile.password.err.length': 'Құпия сөз кемінде 8 таңбадан тұруы керек',
            'profile.password.err.match': 'Құпия сөздер сәйкес келмейді',
            'profile.password.err.same': 'Жаңа құпия сөз ағымдағыдан өзгеше болуы керек',
            'profile.password.err.save': 'Құпия сөзді жаңарту қатесі',

            /* ── Admin Users Management (Sprint 12) ── */
            'admin.users.title': 'Пайдаланушыларды басқару',
            'admin.users.subtitle': 'Әкімшілік құпия сөзді қалпына келтіру және есептік жазба қауіпсіздігі',
            'admin.users.count': 'пайдаланушы',
            'admin.users.btn.create': 'Пайдаланушы қосу',
            'admin.users.col.id': '#',
            'admin.users.col.user': 'Пайдаланушы',
            'admin.users.col.role': 'Рөлі',
            'admin.users.col.status': 'Күйі',
            'admin.users.col.created': 'Тіркелген күні',
            'admin.users.col.lastLogin': 'Соңғы кіру',
            'admin.users.col.actions': 'Әрекеттер',
            'admin.users.role.admin': 'Әкімші',
            'admin.users.role.engineer': 'Инженер',
            'admin.users.status.active': 'Белсенді',
            'admin.users.status.mustChange': 'Құпия сөзді ауыстыру',
            'admin.users.btn.reset': 'Құпия сөзді қалпына келтіру',
            'admin.users.btn.edit': 'Өңдеу',
            'admin.users.btn.delete': 'Жою',
            'admin.users.btn.deleteSelfForbidden': 'Өз есептік жазбаңызды жоюға тыйым салынады',
            'admin.users.protected': 'Қорғалған',
            'admin.users.empty': 'Пайдаланушылар табылмады',
            'admin.users.modal.create.title': 'Жаңа пайдаланушы қосу',
            'admin.users.modal.create.username': 'Логин',
            'admin.users.modal.create.usernamePh': 'Логинді енгізіңіз',
            'admin.users.modal.create.email': 'Электрондық пошта (міндетті емес)',
            'admin.users.modal.create.emailPh': 'user@its-kom.kz',
            'admin.users.modal.create.roleNote': 'Рөлі: Инженер (автоматты түрде тағайындалады)',
            'admin.users.modal.create.submit': 'Пайдаланушыны жасау',
            'admin.users.modal.create.success': 'Пайдаланушы сәтті жасалды',
            'admin.users.modal.edit.title': 'Пайдаланушыны өңдеу',
            'admin.users.modal.edit.roleLocked': 'Рөлі:',
            'admin.users.modal.edit.roleNote': '(рөлді өзгертуге болмайды)',
            'admin.users.modal.edit.save': 'Сақтау',
            'admin.users.modal.edit.success': 'Пайдаланушы деректері сәтті жаңартылды',
            'admin.users.modal.delete.title': 'Пайдаланушыны жою керек пе?',
            'admin.users.modal.delete.desc': 'Пайдаланушының есептік жазбасын шынымен жойғыңыз келе ме:',
            'admin.users.modal.delete.warn': 'Бұл әрекет қайтарылмайды. Пайдаланушының қолжетімділігі мен белсенді сессиялары дереу тоқтатылады.',
            'admin.users.modal.delete.btn': 'Жою',
            'admin.users.modal.delete.success': 'Пайдаланушы сәтті жойылды',
            'admin.users.modal.confirm.title': 'Пайдаланушы құпия сөзін қалпына келтіру',
            'admin.users.modal.confirm.warn': 'Жаңа тұрақты құпия сөз жасалады және пайдаланушының белсенді сессиялары бірден тоқтатылады.',
            'admin.users.modal.confirm.btn': 'Жаңа құпия сөз жасау',
            'admin.users.modal.result.title': 'Жаңа құпия сөз орнатылды',
            'admin.users.modal.result.createdTitle': 'Пайдаланушы жасалды',
            'admin.users.modal.result.copy': 'Көшіру',
            'admin.users.modal.result.copied': 'Көшірілді!',
            'admin.users.modal.result.warn': '⚠️ Құпия сөзді қызметкерге беріңіз немесе сақтап қойыңыз. Ол тек бір рет көрсетіледі.',
            'admin.users.modal.result.close': 'Жабу',

            /* ── Login / Reset Password / Register ── */
            'login.title':              'Білім қоры',
            'login.subtitle':           'Білім қоры',
            'login.username.label':     'Логин',
            'login.username.ph':        'логин',
            'login.password':           'Құпия сөз',
            'login.password.ph':        'құпия сөз',
            'login.forgot':             'Құпия сөзді қалпына келтіру',
            'login.remember':           'Жүйеде қалу',
            'login.btn':                'Кіру',
            'login.reset.step1':        '1-қадам / 2: Есептік жазбаны тексеру',
            'login.reset.step2':        '2-қадам / 2: Жаңа құпия сөз',
            'login.reset.verify':       'Тексеру',
            'login.reset.newPass':      'Жаңа құпия сөз',
            'login.reset.confirmPass':  'Құпия сөзді қайталаңыз',
            'login.reset.savePass':     'Құпия сөзді сақтау',
            'login.reset.back':         'Кіру бетіне оралу',
            'login.reset.user':         'Пайдаланушы:',
            'login.quickHint':          'Жергілікті кіру:',
            'login.quickFill':          'Толтыру',
            'login.quickDone':          '✓ Толтырылды!',
            'login.noAccount':          'Аккаунт жоқ па?',
            'login.register':           'Тіркелу',
            'register.title':           'Жаңа пайдаланушыны тіркеу',
            'register.login.label':     'Кіру аты',
            'register.login.ph':        'Кіру атын енгізіңіз (кемінде 3 таңба)',
            'register.login.hint':      'Жүйеге кіру үшін пайдаланылады',
            'register.pass.label':      'Құпия сөз',
            'register.pass.ph':         'Құпия сөзді енгізіңіз (кемінде 5 таңба)',
            'register.pass.hint':       'Кемінде 5 таңба',
            'register.confirm.label':   'Құпия сөзді растау',
            'register.confirm.ph':      'Құпия сөзді қайталаңыз',
            'register.btn':             'Тіркелу',
            'register.hasAccount':      'Аккаунт бар ма?',
            'register.login':           'Кіру',
            'register.mismatch':        'Құпия сөздер сәйкес келмейді!'
        }
    };

    function t(key) {
        var lang = localStorage.getItem('ui_lang') || 'ru';
        if (lang === 'en') {
            lang = 'ru';
            try { localStorage.setItem('ui_lang', 'ru'); } catch (e) {}
        }
        var dict = T[lang] || T['ru'];
        return dict[key] !== undefined ? dict[key] : (T['ru'][key] || key);
    }

    function tf(key, val) {
        return t(key).replace('%s', val || '');
    }

    function applyLang(lang) {
        if (!lang || lang === 'en') {
            lang = 'ru';
            try { localStorage.setItem('ui_lang', 'ru'); } catch (e) {}
        }
        var dict = T[lang] || T['ru'];

        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            if (dict[key] !== undefined) {
                var textSpan = el.querySelector(':scope > span:not(#filesTabCounter):not(.org-tab-badge):not(.tab-count-badge)') ||
                               el.querySelector('span:not(#filesTabCounter):not(.org-tab-badge):not(.tab-count-badge)');
                if (textSpan && (el.querySelector('svg') || el.querySelector('#filesTabCounter') || el.children.length > 1)) {
                    textSpan.textContent = dict[key];
                } else if (el.children.length === 0) {
                    el.textContent = dict[key];
                } else if (textSpan) {
                    textSpan.textContent = dict[key];
                }
            }
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

        document.documentElement.lang = lang === 'kk' ? 'kk' : 'ru';
    }

    /* ── Theme management ─────────────────────────────── */
    function getSavedTheme() {
        var meta = document.querySelector('meta[name="app-user"]');
        var u = meta && meta.getAttribute('content');
        return localStorage.getItem('ui_theme') || (u ? localStorage.getItem('ui_theme_' + u) : null) || 'light';
    }

    function updateThemeToggleUI(theme) {
        var isDark = theme === 'dark';
        var label = isDark ? 'Переключить на светлую тему' : 'Переключить тему';
        var btns = document.querySelectorAll('.theme-toggle-btn, #themeToggle');
        btns.forEach(function (btn) {
            btn.setAttribute('aria-label', label);
            btn.setAttribute('title', label);
        });
    }

    function applyTheme(theme) {
        var nextTheme = theme === 'dark' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', nextTheme);
        try {
            localStorage.setItem('ui_theme', nextTheme);
            var meta = document.querySelector('meta[name="app-user"]');
            var u = meta && meta.getAttribute('content');
            if (u) {
                localStorage.setItem('ui_theme_' + u, nextTheme);
            }
        } catch (e) {}
        updateThemeToggleUI(nextTheme);
    }

    function initTheme() {
        var saved = getSavedTheme();
        document.documentElement.setAttribute('data-theme', saved);
        updateThemeToggleUI(saved);

        var btns = document.querySelectorAll('.theme-toggle-btn, #themeToggle');
        btns.forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                var current = document.documentElement.getAttribute('data-theme') || 'light';
                applyTheme(current === 'dark' ? 'light' : 'dark');
            });
        });

        window.addEventListener('storage', function (e) {
            if (e.key === 'ui_theme' && e.newValue) {
                document.documentElement.setAttribute('data-theme', e.newValue);
                updateThemeToggleUI(e.newValue);
            }
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

    window.ITS_I18n = { t: t, tf: tf, apply: applyLang, syncTopbar: syncTopbarAvatar, applyTheme: applyTheme, updateThemeToggleUI: updateThemeToggleUI };

    /* Auto-apply on every page load */
    function boot() {
        var lang = localStorage.getItem('ui_lang') || 'ru';
        if (lang === 'en') {
            lang = 'ru';
            try { localStorage.setItem('ui_lang', 'ru'); } catch (e) {}
        }
        applyLang(lang);
        syncTopbarAvatar();
        initTheme();
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

}());
