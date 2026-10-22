require('dotenv').config();
const express = require('express');
const bodyParser = require('body-parser');
const session = require('express-session');
const path = require('path');
const { marked } = require('marked');
const createDOMPurify = require('dompurify');
const { JSDOM } = require('jsdom');
const puppeteer = require('puppeteer');

const USE_STUB = process.env.DB_STUB === 'true' || process.env.DB_STUB === '1';

let pool, initDatabase, registerUser, authenticateUser, getUserById, updateProfile, initOrganizationTable, organizationsDB;

if (USE_STUB) {
    console.log('⚠️  Using in-memory DB stub (DB_STUB=true)');
    const stub = require('./db-stub');
    pool = stub.pool;
    initDatabase = stub.initDatabase;
    registerUser = stub.registerUser;
    authenticateUser = stub.authenticateUser;
    getUserById = stub.getUserById;
    updateProfile = stub.updateProfile;
    initOrganizationTable = stub.initOrganizationTable;
    organizationsDB = stub.organizationsDB;
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
    initOrganizationTable = dbModule.initOrganizationTable;
    organizationsDB = dbModule.organizationsDB;
}

const app = express();
const PORT = process.env.PORT || 3000;

// Required for Plesk/Nginx reverse proxy — makes req.secure and req.ip work correctly
app.set('trust proxy', 1);

// Инициализация базы данных для пользователей
initDatabase().catch(err => {
    console.error('Не удалось инициализировать базу данных пользователей:', err);
});

// Инициализация таблицы организаций
initOrganizationTable().catch(err => {
    console.error('Не удалось инициализировать таблицу организаций:', err);
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

// Страница логина
app.get('/login', (req, res) => {
    if (req.session && req.session.user) {
        return res.redirect('/');
    }
    res.render('login', { error: null });
});

// Обработка логина
app.post('/login', async (req, res) => {
    const { username, password, remember } = req.body;
    
    try {
        const result = await authenticateUser(username, password);
        
        if (result.success) {
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
                    return res.render('login', { error: 'Ошибка сессии. Попробуйте ещё раз.' });
                }
                res.redirect('/');
            });
        } else {
            res.render('login', { 
                error: result.error || 'Ошибка входа' 
            });
        }
    } catch (err) {
        console.error('Ошибка при входе:', err);
        res.render('login', { 
            error: 'Ошибка сервера. Попробуйте позже.' 
        });
    }
});

// Страница регистрации
app.get('/register', (req, res) => {
    if (req.session && req.session.user) {
        return res.redirect('/');
    }
    res.render('register', { error: null, success: null });
});

// Обработка регистрации
app.post('/register', async (req, res) => {
    const { username, password, confirmPassword } = req.body;
    
    try {
        // Проверка совпадения паролей
        if (password !== confirmPassword) {
            return res.render('register', { 
                error: 'Пароли не совпадают',
                success: null
            });
        }
        
        const result = await registerUser(username, password);
        
        if (result.success) {
            req.session.user = {
                id: result.user.id,
                username: result.user.username,
                loginTime: new Date()
            };

            req.session.save((err) => {
                if (err) {
                    console.error('Ошибка сохранения сессии:', err);
                    return res.render('register', { error: 'Ошибка сессии. Попробуйте ещё раз.', success: null });
                }
                res.redirect('/');
            });
        } else {
            res.render('register', { 
                error: result.error || 'Ошибка регистрации',
                success: null
            });
        }
    } catch (err) {
        console.error('Ошибка при регистрации:', err);
        res.render('register', { 
            error: 'Ошибка сервера. Попробуйте позже.',
            success: null
        });
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
            currentDateStr
        });
    } catch (err) {
        console.error('Ошибка рендеринга главной страницы:', err);
        res.render('home', { 
            user: req.session.user,
            serverCount: 25,
            currentDateStr: 'Четверг, 22 октября 2026 г.'
        });
    }
});

// Страница Wiki (список организаций)
app.get('/wiki', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        res.render('wiki', { 
            organizations, 
            currentOrg: null,
            user: req.session.user
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
        
        res.render('wiki', { 
            organizations, 
            currentOrg: organization,
            user: req.session.user
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

// API: Экспорт в TXT
app.get('/api/export/txt/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }
        
        const filename = `${organization.name.replace(/[^a-zA-Z0-9а-яА-Я]/g, '_')}.txt`;
        res.setHeader('Content-Type', 'text/plain; charset=utf-8');
        res.setHeader('Content-Disposition', `attachment; filename="${filename}"`);
        res.send(organization.content);
    } catch (error) {
        console.error('Ошибка экспорта TXT:', error.message);
        return res.status(500).json({ error: 'Ошибка экспорта в TXT' });
    }
});

// API: Экспорт в PDF
app.get('/api/export/pdf/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }
        
        // Конвертация Markdown в HTML
        const rawHtml = marked(organization.content);
        const cleanHtml = DOMPurify.sanitize(rawHtml);
        const cleanName = DOMPurify.sanitize(organization.name);

        // Создание HTML для PDF
        const htmlContent = `
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <title>${cleanName}</title>
                <style>
                    body { font-family: Arial, sans-serif; padding: 20px; }
                    h1 { color: #333; }
                    pre { background: #f4f4f4; padding: 10px; border-radius: 5px; }
                    code { background: #f4f4f4; padding: 2px 5px; }
                </style>
            </head>
            <body>
                <h1>${cleanName}</h1>
                ${cleanHtml}
            </body>
            </html>
        `;
        
        // Генерация PDF
        const browser = await puppeteer.launch({
            headless: true,
            args: ['--no-sandbox', '--disable-setuid-sandbox']
        });
        const page = await browser.newPage();
        await page.setContent(htmlContent, { waitUntil: 'networkidle0' });
        const pdfBuffer = await page.pdf({ 
            format: 'A4',
            margin: { top: '20px', right: '20px', bottom: '20px', left: '20px' }
        });
        await browser.close();
        
        const filename = `${organization.name.replace(/[^a-zA-Z0-9а-яА-Я]/g, '_')}.pdf`;
        res.setHeader('Content-Type', 'application/pdf');
        res.setHeader('Content-Disposition', `attachment; filename="${filename}"`);
        res.send(pdfBuffer);
        
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

module.exports = { app, serverInstance };


