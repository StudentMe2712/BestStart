// Скрипт для заполнения базы данных начальными данными
const { organizationsDB } = require('./database');

// Список организаций из ТЗ (со скриншотов)
const organizations = [
    { name: 'Antarium', content: 'kuadmin123 - Пароль от Ани деск\nKu@dmin123Q - mstsc\n\nAnydesk\n1218740083/antraium/администратор\n\nДля входа на комп\nantraium.local\\администратор\nKu@dmin123Q\n\nданные для создания базы\nкластер серверов - terminal\nСервер базы данных - SQL\nsa\nKu@dmin123Q' },
    { name: 'ALPHUTA', content: '' },
    { name: 'ALTYN TRADE', content: '' },
    { name: 'Alanda', content: '' },
    { name: 'BIRLIK SAUDA', content: '' },
    { name: 'CityTrade', content: '' },
    { name: 'DIVA (Казахстан)', content: '' },
    { name: 'Di Trade', content: '' },
    { name: 'GWS Almaty', content: '' },
    { name: 'GWS Astana', content: '' },
    { name: 'GWS Pavl', content: '' },
    { name: 'GWS Павлодар', content: '' },
    { name: 'HOLZ', content: '' },
    { name: 'MONICO', content: '' },
    { name: 'MYD Production Group', content: '' },
    { name: 'NAK', content: '' },
    { name: 'Samhat', content: '' },
    { name: 'Silvanit', content: '' },
    { name: 'VDS(новый сервер)', content: '' },
    { name: 'Vehi', content: '' },
    { name: 'АСТ-Техносервис', content: '' },
    { name: 'Автоперекресток', content: '' },
    { name: 'Аском', content: '' },
    { name: 'Аудет 7 (ИП Бектуров)', content: '' },
    { name: 'ВДС Казахстан', content: '' },
    { name: 'ГК Sensata (ARG)', content: '' },
    { name: 'Галерея крепежа', content: '' },
    { name: 'Esen (Костанай сервер)', content: '' },
    { name: 'ИП Жуманов', content: '' },
    { name: 'ИП Эльвейн (РостДеталь)', content: '' },
    { name: 'КазпромСнаб', content: '' },
    { name: 'Карта', content: '' },
    { name: 'Курылыс Групп', content: '' },
    { name: 'ООО ПРОМСОЮЗ АЛИБЕТ', content: '' }
];

console.log('🌱 Заполнение базы данных начальными данными...\n');

let completed = 0;
let errors = 0;

organizations.forEach((org, index) => {
    organizationsDB.create(org.name, org.content, function(err) {
        if (err) {
            if (err.message.includes('UNIQUE')) {
                console.log(`⚠️  Организация "${org.name}" уже существует`);
            } else {
                console.error(`❌ Ошибка создания "${org.name}":`, err.message);
                errors++;
            }
        } else {
            console.log(`✓ Создана: ${org.name}`);
            completed++;
        }
        
        // Проверка завершения
        if (index === organizations.length - 1) {
            setTimeout(() => {
                console.log('\n' + '='.repeat(50));
                console.log(`Завершено! Создано: ${completed}, Ошибок: ${errors}`);
                console.log('='.repeat(50));
                process.exit(0);
            }, 500);
        }
    });
});


