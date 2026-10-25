require('dotenv').config();
const express = require('express');
const bodyParser = require('body-parser');
const session = require('express-session');
const path = require('path');
const fs = require('fs');
const multer = require('multer');
const { marked } = require('marked');
const createDOMPurify = require('dompurify');
const { JSDOM } = require('jsdom');
const XLSX = require('xlsx');

const USE_STUB = process.env.DB_STUB === 'true' || process.env.DB_STUB === '1';

let pool, initDatabase, registerUser, authenticateUser, getUserById, updateProfile, checkUserExists, resetPassword, initOrganizationTable, organizationsDB, initDatabases1CTable, databases1CDB, initUserPinsTable, userPinsDB, initFilesTable, filesDB;

if (USE_STUB) {
    console.log('⚠️  Using in-memory DB stub (DB_STUB=true)');
    const stub = require('./db-stub');
    pool = stub.pool;
    initDatabase = stub.initDatabase;
    registerUser = stub.registerUser;
    authenticateUser = stub.authenticateUser;
    getUserById = stub.getUserById;
    updateProfile = stub.updateProfile;
    checkUserExists = stub.checkUserExists;
    resetPassword = stub.resetPassword;
    initOrganizationTable = stub.initOrganizationTable;
    organizationsDB = stub.organizationsDB;
    initDatabases1CTable = stub.initDatabases1CTable;
    databases1CDB = stub.databases1CDB;
    initUserPinsTable = stub.initUserPinsTable;
    userPinsDB = stub.userPinsDB;
    initFilesTable = stub.initFilesTable;
    filesDB = stub.filesDB;
} else {
    const MySQLStore = require('express-mysql-session')(session);
    const dbModule = require('./database');
    const authModule = require('./auth');
    pool = authModule.pool;
    initDatabase = authModule.initDatabase;
    registerUser = authModule.registerUser;
    authenticateUser = authModule.authenticateUser;
    getUserById = authModule.getUserById;
    updateProfile = authModule.updateProfile;
    checkUserExists = authModule.checkUserExists;
    resetPassword = authModule.resetPassword;
    initOrganizationTable = dbModule.initOrganizationTable;
    organizationsDB = dbModule.organizationsDB;
    initDatabases1CTable = dbModule.initDatabases1CTable;
    databases1CDB = dbModule.databases1CDB;
    initUserPinsTable = dbModule.initUserPinsTable;
    userPinsDB = dbModule.userPinsDB;
    initFilesTable = dbModule.initFilesTable;
    filesDB = dbModule.filesDB;
}

const app = express();
const PORT = process.env.PORT || 3000;

// Required for Plesk/Nginx reverse proxy — makes req.secure and req.ip work correctly
app.set('trust proxy', 1);

// Middleware базовых HTTP-заголовков безопасности (Спринт 7)
app.use((req, res, next) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('X-Frame-Options', 'SAMEORIGIN');
    res.setHeader('Referrer-Policy', 'same-origin');
    next();
});

// Инициализация базы данных для пользователей
initDatabase().catch(err => {
    console.error('Не удалось инициализировать базу данных пользователей:', err);
});

// Инициализация таблицы организаций
initOrganizationTable().catch(err => {
    console.error('Не удалось инициализировать таблицу организаций:', err);
});

// Инициализация таблицы баз 1С
initDatabases1CTable().catch(err => {
    console.error('Не удалось инициализировать таблицу баз 1С:', err);
});

// Инициализация таблицы закреплений организаций (Спринт 7)
initUserPinsTable().catch(err => {
    console.error('Не удалось инициализировать таблицу user_pins:', err);
});

// Инициализация таблицы прикрепленных файлов (Спринт 7)
initFilesTable().catch(err => {
    console.error('Не удалось инициализировать таблицу organization_files:', err);
});

// Настройка DOMPurify для безопасности
const window = new JSDOM('').window;
const DOMPurify = createDOMPurify(window);

// Middleware
app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));
app.use(express.static(path.join(__dirname, 'public')));
app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));

// Настройка сессий
let sessionStore;
if (USE_STUB) {
    sessionStore = new session.MemoryStore();
} else {
    const MySQLStore = require('express-mysql-session')(session);
    sessionStore = new MySQLStore({
        host: process.env.DB_HOST || 'localhost',
        port: process.env.DB_PORT || 3306,
        user: process.env.DB_USER || 'p-14802_adminits',
        password: process.env.DB_PASSWORD || 'ASDFzxcv@113525',
        database: process.env.DB_NAME || 'p-14802_wiki',
        clearExpired: true,
        checkExpirationInterval: 900000,
        expiration: 86400000,
        endConnectionOnClose: true,
    }, pool);
}

app.use(session({
    secret: process.env.SESSION_SECRET || 'its-kom-wiki-secret-key-2025-secure',
    store: sessionStore,
    resave: false,
    saveUninitialized: false,
    cookie: {
        maxAge: 30 * 24 * 60 * 60 * 1000,
        httpOnly: true,
        secure: false,   // set to true only if Node.js receives HTTPS directly (no proxy)
        sameSite: 'lax'
    }
}));

// Middleware для проверки авторизации
function requireAuth(req, res, next) {
    if (req.session && req.session.user) {
        next();
    } else {
        res.redirect('/login');
    }
}

// ============================================================
// НАСТРОЙКА MULTER ДЛЯ ЗАГРУЗКИ ФАЙЛОВ ОРГАНИЗАЦИЙ (Спринт 7)
// ============================================================
function fixUtf8Filename(name) {
    if (!name || typeof name !== 'string') return name;
    if (/[^\u0000-\u00ff]/.test(name)) return name;
    try {
        const decoded = Buffer.from(name, 'latin1').toString('utf8');
        if (decoded && !decoded.includes('\uFFFD')) {
            return decoded;
        }
    } catch (e) {}
    return name;
}

const ALLOWED_EXTENSIONS = [
    '.xlsx', '.xls', '.csv', '.pdf', '.docx', '.doc',
    '.png', '.jpg', '.jpeg', '.webp', '.txt', '.zip', '.rdp',
    '.xml'
];

const storage = multer.diskStorage({
    destination: (req, file, cb) => {
        const orgId = req.params.id;
        const uploadDir = path.join(__dirname, 'uploads', `org_${orgId}`);
        if (!fs.existsSync(uploadDir)) {
            fs.mkdirSync(uploadDir, { recursive: true });
        }
        cb(null, uploadDir);
    },
    filename: (req, file, cb) => {
        const uniqueSuffix = `${Date.now()}-${Math.round(Math.random() * 1e9)}`;
        const cleanName = fixUtf8Filename(file.originalname);
        const ext = path.extname(cleanName || file.originalname).toLowerCase();
        cb(null, `${uniqueSuffix}${ext}`);
    }
});

const fileFilter = (req, file, cb) => {
    const ext = path.extname(file.originalname || '').toLowerCase();
    const mime = (file.mimetype || '').toLowerCase();
    if (ALLOWED_EXTENSIONS.includes(ext) || ext === '.xml' || mime === 'application/xml' || mime === 'text/xml') {
        cb(null, true);
    } else {
        cb(new Error(`Недопустимый тип файла (${ext}). Разрешены только: ${ALLOWED_EXTENSIONS.join(', ')}`));
    }
};

const upload = multer({
    storage,
    limits: { fileSize: 25 * 1024 * 1024 }, // 25 МБ
    fileFilter
});

const uploadSingleFile = (req, res, next) => {
    upload.single('file')(req, res, (err) => {
        if (err instanceof multer.MulterError) {
            if (err.code === 'LIMIT_FILE_SIZE') {
                return res.status(400).json({ success: false, error: 'Размер файла превышает допустимый лимит (25 МБ)' });
            }
            return res.status(400).json({ success: false, error: `Ошибка загрузки файла: ${err.message}` });
        } else if (err) {
            return res.status(400).json({ success: false, error: err.message });
        }
        next();
    });
};

// ============================================================
// ВСТРОЕННЫЙ IN-MEMORY RATE LIMITER (Спринт 7)
// Ограничение: максимум 5 неудачных попыток за 15 минут с одного IP.
// Защищаемые маршруты:
// - POST /login
// - POST /api/auth/verify-login
// - POST /api/auth/reset-password
// ============================================================
const RATE_LIMIT_WINDOW_MS = 15 * 60 * 1000; // 15 минут
const RATE_LIMIT_MAX_FAILURES = 5;
const failedAttemptsMap = new Map(); // ip -> [timestamp1, timestamp2, ...]

function getClientIp(req) {
    let ip = req.ip || req.connection?.remoteAddress || req.socket?.remoteAddress || '127.0.0.1';
    if (ip === '::1' || ip === '::ffff:127.0.0.1') {
        ip = '127.0.0.1';
    }
    return ip;
}

function getRecentFailedAttempts(ip) {
    const now = Date.now();
    const attempts = failedAttemptsMap.get(ip) || [];
    const validAttempts = attempts.filter(ts => (now - ts) < RATE_LIMIT_WINDOW_MS);
    if (validAttempts.length > 0) {
        failedAttemptsMap.set(ip, validAttempts);
    } else {
        failedAttemptsMap.delete(ip);
    }
    return validAttempts;
}

function isRateLimited(ip) {
    const attempts = getRecentFailedAttempts(ip);
    return attempts.length >= RATE_LIMIT_MAX_FAILURES;
}

function recordFailedAttempt(ip) {
    const attempts = getRecentFailedAttempts(ip);
    attempts.push(Date.now());
    failedAttemptsMap.set(ip, attempts);
}

function clearRateLimit(ip) {
    failedAttemptsMap.delete(ip);
}

// Middleware проверки Rate Limit
function checkAuthRateLimit(req, res, next) {
    const ip = getClientIp(req);
    if (isRateLimited(ip)) {
        const message = 'Слишком много неудачных попыток. Пожалуйста, повторите попытку через 15 минут.';
        if (req.xhr || req.is('json') || req.headers.accept?.includes('application/json') || req.path.startsWith('/api/')) {
            return res.status(429).json({ success: false, error: message });
        }
        return res.status(429).render('login', {
            error: message,
            isStubMode: USE_STUB
        });
    }
    next();
}

const rateLimitCleanupTimer = setInterval(() => {
    const now = Date.now();
    for (const [ip, timestamps] of failedAttemptsMap.entries()) {
        const valid = timestamps.filter(ts => (now - ts) < RATE_LIMIT_WINDOW_MS);
        if (valid.length === 0) {
            failedAttemptsMap.delete(ip);
        } else {
            failedAttemptsMap.set(ip, valid);
        }
    }
}, 5 * 60 * 1000);
if (rateLimitCleanupTimer.unref) {
    rateLimitCleanupTimer.unref();
}

// Страница логина
app.get('/login', (req, res) => {
    if (req.session && req.session.user) {
        return res.redirect('/');
    }
    res.render('login', { error: null, isStubMode: USE_STUB });
});

// Обработка логина с защитой Rate Limiter
app.post('/login', checkAuthRateLimit, async (req, res) => {
    const { username, password, remember } = req.body;
    const clientIp = getClientIp(req);
    
    try {
        const result = await authenticateUser(username, password);
        
        if (result.success) {
            clearRateLimit(clientIp);
            req.session.user = {
                id: result.user.id,
                username: result.user.username,
                loginTime: new Date()
            };

            if (remember) {
                req.session.cookie.maxAge = 90 * 24 * 60 * 60 * 1000;
            } else {
                req.session.cookie.maxAge = 24 * 60 * 60 * 1000;
            }

            req.session.save((err) => {
                if (err) {
                    console.error('Ошибка сохранения сессии:', err);
                    if (req.is('json') || req.headers.accept?.includes('application/json')) {
                        return res.status(500).json({ success: false, error: 'Ошибка сессии. Попробуйте ещё раз.' });
                    }
                    return res.render('login', { error: 'Ошибка сессии. Попробуйте ещё раз.', isStubMode: USE_STUB });
                }
                if (req.is('json') || req.headers.accept?.includes('application/json')) {
                    return res.json({ success: true, redirect: '/' });
                }
                res.redirect('/');
            });
        } else {
            recordFailedAttempt(clientIp);
            if (req.is('json') || req.headers.accept?.includes('application/json')) {
                return res.status(401).json({ 
                    success: false, 
                    error: result.error || 'Ошибка входа' 
                });
            }
            res.render('login', { 
                error: result.error || 'Ошибка входа',
                isStubMode: USE_STUB 
            });
        }
    } catch (err) {
        console.error('Ошибка при входе:', err);
        recordFailedAttempt(clientIp);
        if (req.is('json') || req.headers.accept?.includes('application/json')) {
            return res.status(500).json({ 
                success: false, 
                error: 'Ошибка сервера. Попробуйте позже.' 
            });
        }
        res.render('login', { 
            error: 'Ошибка сервера. Попробуйте позже.',
            isStubMode: USE_STUB 
        });
    }
});

// ============================================================
// ПУБЛИЧНАЯ РЕГИСТРАЦИЯ ЗАКРЫТА (Спринт 7)
// Публичная регистрация отключена в целях корпоративной безопасности.
// Доступ предоставляется только системным администратором.
// ============================================================
app.get('/register', (req, res) => {
    res.redirect('/login');
});

app.post('/register', (req, res) => {
    res.redirect('/login');
});

// ============================================================
// API ДВУХШАГОВОГО СБРОСА ПАРОЛЯ И ВЕРИФИКАЦИИ (Спринт 7)
// ============================================================

// Шаг 1: Проверка существования логина
app.post('/api/auth/verify-login', checkAuthRateLimit, async (req, res) => {
    const { username } = req.body;
    const clientIp = getClientIp(req);

    if (!username || typeof username !== 'string' || !username.trim()) {
        recordFailedAttempt(clientIp);
        return res.status(404).json({ success: false, error: 'Пользователь не найден' });
    }

    try {
        const check = await checkUserExists(username.trim());
        if (check && check.exists) {
            return res.json({ success: true, exists: true });
        } else {
            recordFailedAttempt(clientIp);
            return res.status(404).json({ success: false, error: 'Пользователь не найден' });
        }
    } catch (err) {
        console.error('Ошибка проверки существования пользователя:', err);
        recordFailedAttempt(clientIp);
        return res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// Шаг 2: Установка нового пароля
app.post('/api/auth/reset-password', checkAuthRateLimit, async (req, res) => {
    const { username, password, confirmPassword } = req.body;
    const clientIp = getClientIp(req);

    if (!username || typeof username !== 'string' || !username.trim()) {
        recordFailedAttempt(clientIp);
        return res.status(400).json({ success: false, error: 'Логин не указан' });
    }

    if (!password || typeof password !== 'string' || password.length < 8) {
        recordFailedAttempt(clientIp);
        return res.status(400).json({ success: false, error: 'Пароль должен содержать минимум 8 символов' });
    }

    if (password !== confirmPassword) {
        recordFailedAttempt(clientIp);
        return res.status(400).json({ success: false, error: 'Пароли не совпадают' });
    }

    try {
        const result = await resetPassword(username.trim(), password);
        if (result.success) {
            clearRateLimit(clientIp);
            return res.json({ success: true, message: 'Пароль успешно изменен' });
        } else {
            recordFailedAttempt(clientIp);
            return res.status(400).json({ success: false, error: result.error || 'Ошибка сброса пароля' });
        }
    } catch (err) {
        console.error('Ошибка сброса пароля:', err);
        recordFailedAttempt(clientIp);
        return res.status(500).json({ success: false, error: 'Ошибка сервера при сбросе пароля' });
    }
});

// Выход
app.get('/logout', (req, res) => {
    req.session.destroy((err) => {
        if (err) {
            console.error('Ошибка при выходе:', err);
        }
        res.redirect('/login');
    });
});

// Профиль пользователя
app.get('/profile', requireAuth, async (req, res) => {
    try {
        const userDetails = await getUserById(req.session.user.id);
        res.render('profile', { user: req.session.user, userDetails });
    } catch (err) {
        console.error('Ошибка профиля:', err);
        res.render('profile', { user: req.session.user, userDetails: null });
    }
});

// Обновление профиля (email)
app.put('/api/profile', requireAuth, async (req, res) => {
    const { email } = req.body;

    if (email && email.trim()) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email.trim())) {
            return res.status(400).json({ error: 'Неверный формат email' });
        }
    }

    try {
        const result = await updateProfile(req.session.user.id, {
            email: email && email.trim() ? email.trim() : null
        });
        if (!result.success) {
            return res.status(500).json({ error: result.error || 'Ошибка обновления' });
        }
        res.json({ success: true });
    } catch (err) {
        console.error('Ошибка обновления профиля:', err);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// Главная страница (защищенная)
app.get('/', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const serverCount = Array.isArray(organizations) ? organizations.length : 25;
        const allDatabases = await databases1CDB.getAll();
        const totalDatabasesCount = allDatabases ? allDatabases.length : 0;
        const userPins = await userPinsDB.getUserPins(req.session.user.id);
        const pinnedOrgs = Array.isArray(organizations) ? organizations.filter(o => (userPins || []).includes(o.id)) : [];
        const now = new Date();
        const formattedDate = now.toLocaleDateString('ru-RU', {
            weekday: 'long',
            day: 'numeric',
            month: 'long',
            year: 'numeric'
        });
        const currentDateStr = formattedDate.charAt(0).toUpperCase() + formattedDate.slice(1);

        res.render('home', { 
            user: req.session.user,
            serverCount,
            totalDatabasesCount,
            currentDateStr,
            userPins: userPins || [],
            pinnedOrgs: pinnedOrgs || [],
            organizations: organizations || []
        });
    } catch (err) {
        console.error('Ошибка рендеринга главной страницы:', err);
        res.render('home', { 
            user: req.session.user,
            serverCount: 25,
            totalDatabasesCount: 4,
            currentDateStr: 'Четверг, 22 октября 2026 г.',
            userPins: [],
            pinnedOrgs: [],
            organizations: []
        });
    }
});

// Вспомогательная функция формирования монограммы организации (Спринт 6)
function getOrgMonogram(name) {
    if (!name || typeof name !== 'string') return '';
    const clean = name.trim();
    if (!clean) return '';
    if (/^астанагенплан/i.test(clean)) return 'АП';

    const words = clean.split(/[\s\-_–—/\\()]+/).filter(w => /[a-zA-Zа-яА-ЯёЁ]/.test(w));
    if (words.length >= 2) {
        const m1 = words[0].match(/[a-zA-Zа-яА-ЯёЁ]/);
        const m2 = words[1].match(/[a-zA-Zа-яА-ЯёЁ]/);
        if (m1 && m2) return (m1[0] + m2[0]).toUpperCase();
    }
    const letters = clean.replace(/[^a-zA-Zа-яА-ЯёЁ]/g, '');
    if (letters.length >= 2) {
        return letters.substring(0, 2).toUpperCase();
    }
    return letters.toUpperCase();
}

// Страница Wiki (список организаций)
app.get('/wiki', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const userPins = await userPinsDB.getUserPins(req.session.user.id);
        res.render('wiki', { 
            organizations, 
            currentOrg: null,
            databases: [],
            user: req.session.user,
            orgMonogram: '',
            userPins: userPins || [],
            files: []
        });
    } catch (err) {
        console.error('Ошибка получения организаций:', err.message);
        return res.status(500).send('Ошибка сервера');
    }
});

// Страница организации (защищенная)
app.get('/wiki/organization/:id', requireAuth, async (req, res) => {
    const orgId = req.params.id;
    
    try {
        const organizations = await organizationsDB.getAll();
        const organization = await organizationsDB.getById(orgId);
        
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }

        const databases = await databases1CDB.getByOrgId(orgId);
        const userPins = await userPinsDB.getUserPins(req.session.user.id);
        const files = await filesDB.getByOrgId(orgId);
        
        res.render('wiki', { 
            organizations, 
            currentOrg: organization,
            databases: databases || [],
            user: req.session.user,
            orgMonogram: getOrgMonogram(organization.name),
            userPins: userPins || [],
            files: files || []
        });
    } catch (err) {
        console.error('Ошибка получения организации:', err.message);
        return res.status(500).send('Ошибка сервера');
    }
});

// API: Получить все организации
app.get('/api/organizations', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        res.json(organizations);
    } catch (err) {
        console.error('Ошибка получения организаций:', err.message);
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Получить организацию по ID
app.get('/api/organizations/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        
        if (!organization) {
            return res.status(404).json({ error: 'Организация не найдена' });
        }
        
        res.json(organization);
    } catch (err) {
        console.error('Ошибка получения организации:', err.message);
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Создать новую организацию
app.post('/api/organizations', requireAuth, async (req, res) => {
    const { name, content } = req.body;
    
    if (!name || name.trim() === '') {
        return res.status(400).json({ error: 'Название организации обязательно' });
    }
    
    try {
        const insertId = await organizationsDB.create(name.trim(), content || '');
        res.json({ 
            id: insertId, 
            name: name.trim(), 
            content: content || '',
            message: 'Организация успешно создана' 
        });
    } catch (err) {
        console.error('Ошибка создания организации:', err.message);
        if (err.message.includes('Duplicate entry')) { // MariaDB specific error for unique constraint
            return res.status(400).json({ error: 'Организация с таким названием уже существует' });
        }
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Обновить организацию
app.put('/api/organizations/:id', requireAuth, async (req, res) => {
    const { name, content } = req.body;
    const id = req.params.id;
    
    if (!name || name.trim() === '') {
        return res.status(400).json({ error: 'Название организации обязательно' });
    }
    
    try {
        const affectedRows = await organizationsDB.update(id, name.trim(), content || '');
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'Организация не найдена' });
        }
        res.json({ message: 'Организация успешно обновлена' });
    } catch (err) {
        console.error('Ошибка обновления организации:', err.message);
        if (err.message.includes('Duplicate entry')) { // MariaDB specific error for unique constraint
            return res.status(400).json({ error: 'Организация с таким названием уже существует' });
        }
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Удалить организацию
app.delete('/api/organizations/:id', requireAuth, async (req, res) => {
    try {
        const affectedRows = await organizationsDB.delete(req.params.id);
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'Организация не найдена' });
        }
        res.json({ message: 'Организация успешно удалена' });
    } catch (err) {
        console.error('Ошибка удаления организации:', err.message);
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// ============================================================
// API: ЗАКРЕПЛЕНИЕ ОРГАНИЗАЦИЙ (USER PINS — СПРИНТ 7)
// ============================================================

// Переключить закрепление организации
app.post('/api/organizations/:id/pin', requireAuth, async (req, res) => {
    try {
        const orgId = Number(req.params.id);
        const userId = req.session.user.id;
        const result = await userPinsDB.toggleUserPin(userId, orgId);
        res.json({
            success: true,
            pinned: result.pinned,
            pins: result.pins
        });
    } catch (err) {
        console.error('Ошибка переключения закрепления организации:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// Получить список ID закрепленных организаций текущего пользователя
app.get('/api/user/pins', requireAuth, async (req, res) => {
    try {
        const userId = req.session.user.id;
        const pins = await userPinsDB.getUserPins(userId);
        res.json({
            success: true,
            pins: pins || []
        });
    } catch (err) {
        console.error('Ошибка получения списка закреплений пользователя:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// ============================================================
// API: ПРИКРЕПЛЕННЫЕ ФАЙЛЫ (ORGANIZATION FILES — СПРИНТ 7)
// ============================================================

// Загрузить файл для организации
app.post('/api/organizations/:id/files', requireAuth, uploadSingleFile, async (req, res) => {
    try {
        if (!req.file) {
            return res.status(400).json({ success: false, error: 'Файл не прикреплен' });
        }
        const orgId = Number(req.params.id);
        const cleanOriginalName = fixUtf8Filename(req.file.originalname);
        const ext = path.extname(cleanOriginalName || '').toLowerCase();
        let mimeType = req.file.mimetype || 'application/octet-stream';
        if (ext === '.xml' && (!req.file.mimetype || req.file.mimetype === 'application/octet-stream')) {
            mimeType = 'application/xml';
        }
        const createdFile = await filesDB.create({
            org_id: orgId,
            filename: req.file.filename,
            original_name: cleanOriginalName,
            mime_type: mimeType,
            file_size: req.file.size,
            uploaded_by: req.session.user.username
        });
        res.json({
            success: true,
            file: createdFile
        });
    } catch (err) {
        console.error('Ошибка загрузки файла организации:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при загрузке файла' });
    }
});

// Получить список файлов организации
app.get('/api/organizations/:id/files', requireAuth, async (req, res) => {
    try {
        const orgId = Number(req.params.id);
        const files = await filesDB.getByOrgId(orgId);
        res.json({
            success: true,
            files: files || []
        });
    } catch (err) {
        console.error('Ошибка получения списка файлов организации:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// Просмотреть файл (инлайн / lightbox)
app.get('/api/files/:fileId/view', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ success: false, error: 'Файл не найден на сервере' });
        }

        res.setHeader('X-Frame-Options', 'SAMEORIGIN');
        res.setHeader('Content-Security-Policy', "frame-ancestors 'self'");
        res.setHeader('Content-Type', file.mime_type || 'application/octet-stream');
        res.setHeader('Content-Disposition', `inline; filename*=UTF-8''${encodeURIComponent(file.original_name)}`);
        res.sendFile(filePath);
    } catch (err) {
        console.error('Ошибка просмотра файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при открытии файла' });
    }
});

// Предварительный просмотр файла (таблицы Excel/CSV, XML, TXT, PDF, изображения)
app.get('/api/files/:fileId/preview', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        if (isNaN(fileId)) {
            return res.status(400).json({ success: false, error: 'Неверный идентификатор файла' });
        }

        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ success: false, error: 'Файл не найден на сервере' });
        }

        const ext = path.extname(file.original_name || file.filename || '').toLowerCase();
        const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.svg', '.bmp'];
        const isImage = IMAGE_EXTENSIONS.includes(ext) || (file.mime_type && file.mime_type.startsWith('image/'));

        // Spreadsheet (.xlsx, .xls, .csv)
        if (ext === '.xlsx' || ext === '.xls' || ext === '.csv') {
            try {
                const workbook = XLSX.readFile(filePath);
                const sheetNames = workbook.SheetNames || [];
                const sheetName = (req.query.sheet && sheetNames.includes(req.query.sheet)) ? req.query.sheet : sheetNames[0];
                const worksheet = sheetName ? workbook.Sheets[sheetName] : null;
                const htmlTable = worksheet ? XLSX.utils.sheet_to_html(worksheet, { id: 'spreadsheetPreviewTable', editable: false }) : '';
                return res.json({
                    success: true,
                    type: 'excel',
                    html: htmlTable,
                    sheetNames: sheetNames,
                    currentSheet: sheetName,
                    originalName: file.original_name
                });
            } catch (parseErr) {
                return res.status(500).json({ success: false, error: 'Ошибка разбора таблицы: ' + parseErr.message });
            }
        }

        // XML (.xml) или текстовые (.txt)
        if (ext === '.xml' || ext === '.txt') {
            try {
                const content = fs.readFileSync(filePath, 'utf-8');
                return res.json({
                    success: true,
                    type: ext === '.xml' ? 'xml' : 'text',
                    content: content,
                    originalName: file.original_name
                });
            } catch (readErr) {
                return res.status(500).json({ success: false, error: 'Ошибка чтения файла: ' + readErr.message });
            }
        }

        // PDF или изображение
        if (ext === '.pdf' || isImage) {
            return res.json({
                success: true,
                type: ext === '.pdf' ? 'pdf' : 'image',
                viewUrl: `/api/files/${fileId}/view`,
                originalName: file.original_name
            });
        }

        // Неподдерживаемый тип для инлайн-просмотра
        return res.json({
            success: false,
            type: 'unsupported',
            originalName: file.original_name,
            downloadUrl: `/api/files/${fileId}/download`
        });
    } catch (err) {
        console.error('Ошибка предварительного просмотра файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при предварительном просмотре файла' });
    }
});

// Скачать файл
app.get('/api/files/:fileId/download', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ success: false, error: 'Файл не найден на сервере' });
        }

        res.download(filePath, file.original_name);
    } catch (err) {
        console.error('Ошибка скачивания файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при скачивании файла' });
    }
});

// Удалить файл
app.delete('/api/files/:fileId', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (fs.existsSync(filePath)) {
            try {
                fs.unlinkSync(filePath);
            } catch (unlinkErr) {
                console.warn('Предупреждение при удалении файла с диска:', unlinkErr.message);
            }
        }

        await filesDB.delete(fileId);
        res.json({ success: true });
    } catch (err) {
        console.error('Ошибка удаления файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при удалении файла' });
    }
});

// ============================================================
// API: БАЗЫ ДАННЫХ 1С (ЗАКОНСЕРВИРОВАНО В СПРИНТЕ 7)
// ============================================================

/*
// API: Получить все базы 1С организации
app.get('/api/organizations/:id/databases', requireAuth, async (req, res) => {
    try {
        const databases = await databases1CDB.getByOrgId(req.params.id);
        res.json(databases || []);
    } catch (err) {
        console.error('Ошибка получения баз 1С организации:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Добавить базу 1С к организации
app.post('/api/organizations/:id/databases', requireAuth, async (req, res) => {
    const { name, release, description, updated_at } = req.body;
    if (!name || !name.trim()) {
        return res.status(400).json({ error: 'Наименование базы обязательно' });
    }
    const descText = (description !== undefined ? description : (release || '')).trim();
    try {
        const newDb = await databases1CDB.create({
            org_id: req.params.id,
            name: name.trim(),
            release: descText,
            description: descText,
            updated_at: updated_at || new Date()
        });
        res.status(201).json(newDb);
    } catch (err) {
        console.error('Ошибка добавления базы 1С:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Обновить базу 1С
app.put('/api/databases/:id', requireAuth, async (req, res) => {
    const { name, release, description, updated_at } = req.body;
    if (!name || !name.trim()) {
        return res.status(400).json({ error: 'Наименование базы обязательно' });
    }
    const descText = description !== undefined ? description : release;
    try {
        const affectedRows = await databases1CDB.update(req.params.id, {
            name: name.trim(),
            release: descText !== undefined ? descText.trim() : '',
            description: descText !== undefined ? descText.trim() : '',
            updated_at: updated_at || new Date()
        });
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'База не найдена' });
        }
        res.json({ success: true, message: 'База 1С успешно обновлена' });
    } catch (err) {
        console.error('Ошибка обновления базы 1С:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Удалить базу 1С
app.delete('/api/databases/:id', requireAuth, async (req, res) => {
    try {
        const affectedRows = await databases1CDB.delete(req.params.id);
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'База не найдена' });
        }
        res.json({ success: true, message: 'База 1С успешно удалена' });
    } catch (err) {
        console.error('Ошибка удаления базы 1С:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});
*/

// ============================================================
// СОБЫТИЯ (Журнал обновлений и релизов 1С) (ЗАКОНСЕРВИРОВАНО В СПРИНТЕ 7)
// ============================================================

/*
app.get('/events', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const allDatabases = typeof databases1CDB.getAll === 'function'
            ? await databases1CDB.getAll()
            : await databases1CDB.getAllRecentUpdates();
        const updates = await databases1CDB.getAllRecentUpdates();

        // Группировка баз по организациям
        const dbsByOrg = {};
        (allDatabases || []).forEach(db => {
            const orgId = Number(db.org_id);
            if (!dbsByOrg[orgId]) dbsByOrg[orgId] = [];
            dbsByOrg[orgId].push(db);
        });

        const groupedOrganizations = (organizations || []).map(org => {
            const dbs = dbsByOrg[org.id] || [];
            return {
                id: org.id,
                name: org.name,
                databases: dbs,
                count: dbs.length
            };
        });

        const totalOrgs = groupedOrganizations.length;
        const totalBases = (allDatabases || []).length;

        res.render('events', {
            organizations: organizations || [],
            groupedOrganizations,
            updates: updates || [],
            totalOrgs,
            totalBases,
            user: req.session.user
        });
    } catch (err) {
        console.error('Ошибка рендеринга страницы событий:', err.message);
        res.status(500).send('Ошибка сервера');
    }
});
*/

// Helper: Очистка HTML до чистого моноширинного текста с сохранением всех строк
function cleanHtmlToPlainText(html) {
    if (!html) return '';
    return html
        .replace(/<style[^>]*>[\s\S]*?<\/style>/gi, '')
        .replace(/<script[^>]*>[\s\S]*?<\/script>/gi, '')
        .replace(/<br\s*[\/]?>/gi, '\n')
        .replace(/<\/p>/gi, '\n')
        .replace(/<\/div>/gi, '\n')
        .replace(/<\/li>/gi, '\n')
        .replace(/<[^>]+>/g, '')
        .replace(/&nbsp;/g, ' ')
        .replace(/&amp;/g, '&')
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&quot;/g, '"')
        .replace(/&#39;/g, "'")
        .trim();
}

// Helper: Безопасное экранирование текста для HTML
function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// API: Экспорт в TXT
app.get('/api/export/txt/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }
        
        const cleanText = cleanHtmlToPlainText(organization.content || '');
        const databases = await databases1CDB.getByOrgId(organization.id);

        let fullText = cleanText;
        if (databases && Array.isArray(databases) && databases.length > 0) {
            fullText += '\n\n' + '='.repeat(50) + '\nИНФОРМАЦИОННЫЕ БАЗЫ 1С:\n' + '='.repeat(50) + '\n';
            databases.forEach((db, idx) => {
                const desc = db.description || db.release || '—';
                const dateStr = db.updated_at ? new Date(db.updated_at).toLocaleDateString('ru-RU') : '—';
                fullText += `${idx + 1}. ${db.name}\n   Описание: ${desc}\n   Дата: ${dateStr}\n`;
            });
        }

        const safeName = organization.name.replace(/[/\\?%*:|"<>]/g, '_');
        const filename = `${safeName}.txt`;
        const encodedFilename = encodeURIComponent(filename);

        res.setHeader('Content-Type', 'text/plain; charset=utf-8');
        res.setHeader('Content-Disposition', `attachment; filename="${encodedFilename}"; filename*=UTF-8''${encodedFilename}`);
        res.send(fullText);
    } catch (error) {
        console.error('Ошибка экспорта TXT:', error.message);
        return res.status(500).json({ error: 'Ошибка экспорта в TXT' });
    }
});

// API: Экспорт в PDF (Printable View с @media print и window.print())
app.get('/api/export/pdf/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }

        const databases = await databases1CDB.getByOrgId(organization.id);
        organization.bases = databases || [];

        const user = req.session.user || { username: 'admin' };
        const author = user.username === 'admin' ? 'Администратор' : user.username;
        const cleanAuthor = escapeHtml(author);
        const cleanName = escapeHtml(organization.name);
        const cleanContent = escapeHtml(cleanHtmlToPlainText(organization.content || ''));
        
        const exportDate = new Date().toLocaleString('ru-RU', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });

        let basesHtml = '';
        if (organization.bases && Array.isArray(organization.bases) && organization.bases.length > 0) {
            basesHtml = `
                <div class="print-section-title">Информационные базы 1С</div>
                <table class="print-table">
                    <thead>
                        <tr>
                            <th>Информационная база</th>
                            <th>Описание</th>
                            <th>Дата обновления</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${organization.bases.map(b => `
                            <tr>
                                <td>${escapeHtml(b.name || '')}</td>
                                <td>${escapeHtml(b.description || b.release || '—')}</td>
                                <td>${escapeHtml(b.updated_at ? new Date(b.updated_at).toLocaleDateString('ru-RU') : '—')}</td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            `;
        }

        const htmlContent = `<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${cleanName} — Печать / Сохранить в PDF</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
    <style>
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }
        body {
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: #f1f5f9;
            color: #0f172a;
            line-height: 1.5;
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
        }
        .print-topbar {
            position: sticky;
            top: 0;
            z-index: 100;
            display: flex;
            align-items: center;
            justify-content: space-between;
            background: #ffffff;
            border-bottom: 1px solid #e2e8f0;
            padding: 12px 24px;
            box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
        }
        .print-back-link {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            color: #475569;
            text-decoration: none;
            font-size: 14px;
            font-weight: 500;
            transition: color 0.15s;
        }
        .print-back-link:hover {
            color: #0f172a;
        }
        .print-btn {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: #2563eb;
            color: #ffffff;
            border: none;
            border-radius: 6px;
            padding: 8px 18px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            box-shadow: 0 1px 2px rgba(37, 99, 235, 0.2);
            transition: background 0.15s;
        }
        .print-btn:hover {
            background: #1d4ed8;
        }
        .print-wrapper {
            max-width: 860px;
            margin: 32px auto;
            padding: 0 20px 40px;
        }
        .print-page {
            background: #ffffff;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 48px;
            box-shadow: 0 4px 12px rgba(15, 23, 42, 0.04);
        }
        .print-header {
            border-bottom: 2px solid #0f172a;
            padding-bottom: 16px;
            margin-bottom: 24px;
        }
        .print-title {
            font-size: 26px;
            font-weight: 700;
            color: #0f172a;
            margin-bottom: 8px;
        }
        .print-meta {
            font-size: 13px;
            color: #64748b;
            display: flex;
            gap: 16px;
            flex-wrap: wrap;
        }
        .print-section-title {
            font-size: 13px;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: #64748b;
            font-weight: 700;
            margin-bottom: 10px;
            margin-top: 24px;
        }
        .print-notes {
            font-family: 'JetBrains Mono', Consolas, 'SF Mono', Monaco, monospace;
            font-size: 13px;
            line-height: 1.6;
            white-space: pre-wrap;
            word-break: break-word;
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 20px;
            color: #1e293b;
        }
        .print-table {
            width: 100%;
            border-collapse: collapse;
            font-size: 13px;
            margin-top: 8px;
        }
        .print-table th, .print-table td {
            padding: 10px 14px;
            border: 1px solid #cbd5e1;
            text-align: left;
        }
        .print-table th {
            background: #f1f5f9;
            font-weight: 600;
            color: #1e293b;
        }
        .print-table tr:nth-child(even) td {
            background: #f8fafc;
        }
        @media print {
            body {
                background: #ffffff !important;
                color: #000000 !important;
            }
            .no-print, .print-topbar {
                display: none !important;
            }
            .print-wrapper {
                max-width: 100% !important;
                margin: 0 !important;
                padding: 0 !important;
            }
            .print-page {
                border: none !important;
                box-shadow: none !important;
                padding: 0 !important;
                border-radius: 0 !important;
            }
            .print-header {
                border-bottom: 2px solid #000000 !important;
            }
            .print-notes {
                background: #ffffff !important;
                border: 1px solid #999999 !important;
                color: #000000 !important;
                font-size: 10pt !important;
                line-height: 1.5 !important;
                padding: 12px !important;
            }
            .print-table th {
                background: #eeeeee !important;
                color: #000000 !important;
            }
            @page {
                margin: 15mm 15mm 15mm 15mm;
                size: A4 portrait;
            }
        }
    </style>
</head>
<body>
    <div class="print-topbar no-print">
        <a href="/wiki/organization/${organization.id}" class="print-back-link">← Вернуться в Wiki</a>
        <button onclick="window.print()" class="print-btn">🖨 Напечатать / Сохранить как PDF</button>
    </div>

    <div class="print-wrapper">
        <div class="print-page">
            <header class="print-header">
                <h1 class="print-title">${cleanName}</h1>
                <div class="print-meta">
                    <span><strong>Дата экспорта:</strong> ${exportDate}</span>
                    <span><strong>Автор:</strong> ${cleanAuthor}</span>
                    <span><strong>Система:</strong> ИТС-Ком Wiki</span>
                </div>
            </header>

            <div class="print-section-title">Параметры и заметки сервера</div>
            <pre class="print-notes">${cleanContent || 'Заметки не заполнены'}</pre>

            ${basesHtml}
        </div>
    </div>

    <script>
        window.addEventListener('DOMContentLoaded', () => {
            setTimeout(() => { window.print(); }, 400);
        });
    </script>
</body>
</html>`;

        res.setHeader('Content-Type', 'text/html; charset=utf-8');
        res.send(htmlContent);
        
    } catch (error) {
        console.error('Ошибка экспорта PDF:', error.message);
        res.status(500).json({ error: 'Ошибка экспорта в PDF' });
    }
});

// Запуск сервера
const serverInstance = app.listen(PORT, () => {
    console.log(`\n${'='.repeat(50)}`);
    console.log(`  🚀 Wiki-сервер запущен успешно!`);
    console.log(`  📍 URL: http://localhost:${PORT}`);
    console.log(`  📍 Локальная сеть: http://<IP-адрес>:${PORT}`);
    console.log(`${'='.repeat(50)}\n`);
});

module.exports = {
    app,
    serverInstance,
    getOrgMonogram,
    checkAuthRateLimit,
    isRateLimited,
    clearRateLimit,
    recordFailedAttempt,
    failedAttemptsMap,
    userPinsDB,
    filesDB
};


