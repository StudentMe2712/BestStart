const mysql = require('mysql2/promise');
const bcrypt = require('bcrypt');

// Подключение к MariaDB
const pool = mysql.createPool({
    host: process.env.DB_HOST || 'localhost',
    port: process.env.DB_PORT || 3306,
    user: process.env.DB_USER || 'p-14802_adminits',
    password: process.env.DB_PASSWORD || 'ASDFzxcv@113525',
    database: process.env.DB_NAME || 'p-14802_wiki',
    charset: 'utf8mb4',
    waitForConnections: true,
    connectionLimit: 10,
    queueLimit: 0
});


// Инициализация таблицы пользователей
async function initDatabase() {
    let connection;
    try {
        connection = await pool.getConnection();

        await connection.query(`
            CREATE TABLE IF NOT EXISTS users (
                id INT AUTO_INCREMENT PRIMARY KEY,
                username VARCHAR(50) UNIQUE NOT NULL,
                password_hash VARCHAR(255) NOT NULL,
                email VARCHAR(255) DEFAULT NULL,
                role VARCHAR(20) NOT NULL DEFAULT 'engineer',
                must_change_password TINYINT(1) NOT NULL DEFAULT 0,
                token_version INT UNSIGNED NOT NULL DEFAULT 1,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                last_login TIMESTAMP
            )
        `);

        // Идемпотентные миграции колонок для существующей таблицы users
        const alterStatements = [
            { col: 'email', sql: "ALTER TABLE users ADD COLUMN email VARCHAR(255) DEFAULT NULL" },
            { col: 'role', sql: "ALTER TABLE users ADD COLUMN role VARCHAR(20) NOT NULL DEFAULT 'engineer'" },
            { col: 'must_change_password', sql: "ALTER TABLE users ADD COLUMN must_change_password TINYINT(1) NOT NULL DEFAULT 0" },
            { col: 'token_version', sql: "ALTER TABLE users ADD COLUMN token_version INT UNSIGNED NOT NULL DEFAULT 1" }
        ];

        for (const stmt of alterStatements) {
            try {
                await connection.query(stmt.sql);
            } catch (alterErr) {
                // Игнорируем ошибку существующей колонки (1060 / ER_DUP_FIELDNAME)
                if (alterErr.errno !== 1060 && !alterErr.message.includes('Duplicate column')) {
                    console.warn(`Примечание к миграции колонки ${stmt.col}:`, alterErr.message);
                }
            }
        }

        // Назначение роли администратора для штатной учетной записи 'admin'
        await connection.query("UPDATE users SET role = 'admin' WHERE username = 'admin'");
        // Всем остальным пользователям без роли назначаем роль 'engineer'
        await connection.query("UPDATE users SET role = 'engineer' WHERE username != 'admin' AND (role IS NULL OR role = '')");

        console.log('✓ Таблица users готова (Sprint 12: роли и сессионные токены)');
        
        const [rows] = await connection.query('SELECT COUNT(*) as count FROM users');
        const count = rows[0].count;
        
        if (count === 0) {
            const adminPassword = await bcrypt.hash('admin123', 10);
            await connection.query(
                'INSERT INTO users (username, password_hash, role, must_change_password, token_version) VALUES (?, ?, ?, ?, ?)',
                ['admin', adminPassword, 'admin', 0, 1]
            );
            console.log('✓ Создан администратор по умолчанию (admin / admin123)');
        }
        
    } catch (err) {
        console.error('Ошибка инициализации базы данных:', err.message);
        throw err;
    } finally {
        if (connection) connection.release();
    }
}

// Регистрация нового пользователя
async function registerUser(username, password) {
    let connection;
    try {
        connection = await pool.getConnection();

        const [checkResult] = await connection.query(
            'SELECT id FROM users WHERE username = ?',
            [username]
        );
        
        if (checkResult.length > 0) {
            return { success: false, error: 'Пользователь с таким логином уже существует' };
        }
        
        if (!username || username.trim().length < 3) {
            return { success: false, error: 'Логин должен содержать минимум 3 символа' };
        }
        
        if (!password || password.length < 5) {
            return { success: false, error: 'Пароль должен содержать минимум 5 символов' };
        }
        
        const passwordHash = await bcrypt.hash(password, 10);
        
        const [result] = await connection.query(
            'INSERT INTO users (username, password_hash) VALUES (?, ?)',
            [username.trim(), passwordHash]
        );
        
        const [newUser] = await connection.query(
            'SELECT id, username, created_at FROM users WHERE id = ?',
            [result.insertId]
        );

        console.log(`✓ Зарегистрирован новый пользователь: ${username}`);
        
        return { 
            success: true, 
            user: newUser[0]
        };
        
    } catch (err) {
        console.error('Ошибка регистрации пользователя:', err.message);
        return { success: false, error: 'Ошибка сервера при регистрации' };
    } finally {
        if (connection) connection.release();
    }
}

// Аутентификация пользователя (Спринт 12: роли, признак смены пароля и версия сессии)
async function authenticateUser(username, password) {
    let connection;
    try {
        connection = await pool.getConnection();

        const [rows] = await connection.query(
            'SELECT id, username, password_hash, role, must_change_password, token_version FROM users WHERE username = ?',
            [username]
        );
        
        if (rows.length === 0) {
            return { success: false, error: 'Неверный логин или пароль' };
        }
        
        const user = rows[0];
        
        const passwordMatch = await bcrypt.compare(password, user.password_hash);
        
        if (!passwordMatch) {
            return { success: false, error: 'Неверный логин или пароль' };
        }
        
        await connection.query(
            'UPDATE users SET last_login = CURRENT_TIMESTAMP WHERE id = ?',
            [user.id]
        );
        
        console.log(`✓ Успешный вход: ${username}`);
        
        return { 
            success: true, 
            user: {
                id: user.id,
                username: user.username,
                role: user.role || 'engineer',
                must_change_password: Boolean(user.must_change_password),
                token_version: user.token_version || 1
            }
        };
        
    } catch (err) {
        console.error('Ошибка аутентификации:', err.message);
        return { success: false, error: 'Ошибка сервера при входе' };
    } finally {
        if (connection) connection.release();
    }
}

// Получение пользователя по ID (Спринт 12: выборка роли, обязательной смены и версии токена)
async function getUserById(userId) {
    let connection;
    try {
        connection = await pool.getConnection();

        const [rows] = await connection.query(
            'SELECT id, username, email, role, must_change_password, token_version, created_at, last_login FROM users WHERE id = ?',
            [userId]
        );
        
        if (rows.length === 0) {
            return null;
        }
        
        return rows[0];
        
    } catch (err) {
        console.error('Ошибка получения пользователя:', err.message);
        return null;
    } finally {
        if (connection) connection.release();
    }
}

// Обновление профиля пользователя
async function updateProfile(userId, { email }) {
    let connection;
    try {
        connection = await pool.getConnection();
        await connection.query(
            'UPDATE users SET email = ? WHERE id = ?',
            [email ? email.trim() : null, userId]
        );
        return { success: true };
    } catch (err) {
        console.error('Ошибка обновления профиля:', err.message);
        return { success: false, error: 'Ошибка сервера при обновлении профиля' };
    } finally {
        if (connection) connection.release();
    }
}

// Получение всех пользователей (для закрытой админки, Спринт 12)
// Примечание: password_hash и сессионные секреты категорически исключены из выборки!
async function getAllUsers() {
    let connection;
    try {
        connection = await pool.getConnection();

        const [rows] = await connection.query(
            'SELECT id, username, email, role, must_change_password, created_at, last_login FROM users ORDER BY id ASC'
        );
        
        return rows;
        
    } catch (err) {
        console.error('Ошибка получения пользователей:', err.message);
        return [];
    } finally {
        if (connection) connection.release();
    }
}

// Административный сброс пароля целевого пользователя (Спринт 12)
async function adminResetPassword(targetUserId, tempPassword) {
    if (!targetUserId || !tempPassword) {
        return { success: false, error: 'Не указаны обязательные параметры' };
    }
    let connection;
    try {
        connection = await pool.getConnection();

        const [users] = await connection.query(
            'SELECT id, username, role, token_version FROM users WHERE id = ?',
            [targetUserId]
        );

        if (users.length === 0) {
            return { success: false, error: 'Пользователь не найден' };
        }

        const targetUser = users[0];
        if (targetUser.role === 'admin') {
            return { success: false, error: 'Сброс пароля учётной записи администратора запрещён' };
        }

        const passwordHash = await bcrypt.hash(tempPassword, 10);
        const nextTokenVersion = (targetUser.token_version || 1) + 1;

        await connection.query(
            'UPDATE users SET password_hash = ?, must_change_password = 1, token_version = ? WHERE id = ?',
            [passwordHash, nextTokenVersion, targetUserId]
        );

        return {
            success: true,
            user: {
                id: targetUser.id,
                username: targetUser.username
            }
        };
    } catch (err) {
        console.error('Ошибка административного сброса пароля:', err.message);
        return { success: false, error: 'Ошибка сервера при сбросе пароля' };
    } finally {
        if (connection) connection.release();
    }
}

// Безопасная смена пароля пользователем (Спринт 12)
async function changeUserPassword(userId, currentPassword, newPassword) {
    if (!userId || !currentPassword || !newPassword) {
        return { success: false, error: 'Заполните все обязательные поля' };
    }
    if (typeof newPassword !== 'string' || newPassword.length < 8) {
        return { success: false, error: 'Новый пароль должен содержать минимум 8 символов' };
    }
    if (currentPassword === newPassword) {
        return { success: false, error: 'Новый пароль должен отличаться от текущего' };
    }

    let connection;
    try {
        connection = await pool.getConnection();

        const [rows] = await connection.query(
            'SELECT id, username, password_hash, token_version FROM users WHERE id = ?',
            [userId]
        );

        if (rows.length === 0) {
            return { success: false, error: 'Пользователь не найден' };
        }

        const user = rows[0];
        const match = await bcrypt.compare(currentPassword, user.password_hash);
        if (!match) {
            return { success: false, error: 'Неверный текущий пароль' };
        }

        const newHash = await bcrypt.hash(newPassword, 10);
        const nextTokenVersion = (user.token_version || 1) + 1;

        await connection.query(
            'UPDATE users SET password_hash = ?, must_change_password = 0, token_version = ? WHERE id = ?',
            [newHash, nextTokenVersion, userId]
        );

        return {
            success: true,
            nextTokenVersion
        };
    } catch (err) {
        console.error('Ошибка смены пароля:', err.message);
        return { success: false, error: 'Ошибка сервера при смене пароля' };
    } finally {
        if (connection) connection.release();
    }
}

module.exports = {
    pool,
    initDatabase,
    registerUser,
    authenticateUser,
    getUserById,
    updateProfile,
    getAllUsers,
    adminResetPassword,
    changeUserPassword
};

