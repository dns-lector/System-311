using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_311.Tasks
{
    internal class TasksDemo
    {
        public void Run()
        {
            int w, c;
            ThreadPool.GetMinThreads(out w, out c);
            Console.WriteLine("Tasks Demo {0} {1}", w, c);

            _Run().Wait();      // Перехід до async методу            

            //Console.WriteLine(Task4("Name 5", 5).Result);   // У синх. Result очікує

            Console.WriteLine("TasksDemo Finished");
        }

        private async Task _Run()
        {
            // Task1();                              // Синхронний запуск (хоча метод помічений async)
            var task1 = Task.Run(Task1);          // Асинхронний запуск
            var work1 = Task.Run(Work1);          // Асинхронний запуск (метод НЕ async)
            var task2 = new Task(Task2);          // Немає запуску
            var work2 = new Task(Work2);          // Немає запуску
            task2.Start();
            work2.Start();

            var task3 = Task3("Task3", 10);       // ~ Task.Run(Task3);
            // На відміну від потоків, до задач можна передавати аргументи
            // у звичайному "процедурному" стилі - довільна кількість, тип.
            // Але виклик залишається асинхронним, без очікування може бути
            // скасований завершенням програми (хоча синтаксис виклику 
            // відповідає звичайному, синхронному)

            var ret = Task4("Task 4", 4);      // ret - не String, а Task<String>
            Console.WriteLine( ret.Result );   // В асинхронному контексті Result треба очікувати
            var res = await ret;               // await - і дочекатись, і вилучити результат
            Console.WriteLine(res);

            await Task4("First task", 10)   // Task<"Hello from 'First task' 10">                
                .ContinueWith(task => Task5(task.Result))
                .Unwrap()
                .ContinueWith(task => Console.WriteLine(task.Result));

            Console.WriteLine(              // Виклик синхронних методів не здійснює
                await Task                  // додаткову "обгортку" Task<...>
                .Run(LoadConfig)            // і, відповідно, немає потреби в
                .ContinueWith(ConnectDB)    // .Unwrap()
                .ContinueWith(SyncDB)       // 
            );                              // 

            await Task.WhenAll(task1, task2, work1, work2, task3, ret);
        }

        private String LoadConfig()
        {
            return "Config string ";
        }

        private String ConnectDB(Task<String> configTask)
        {
            return "Connected with " + configTask.Result;
        }
        private String SyncDB(Task<String> connectTask)
        {
            return "Sync by " + connectTask.Result;
        }

        private async void Task1()
        {
            Console.WriteLine("Hello from task1");
        }

        private void Work1()
        {
            Console.WriteLine("Hello from Work1");
        }
        
        private async void Task2()
        {
            Console.WriteLine("Hello from task2");
        }

        private void Work2()
        {
            Console.WriteLine("Hello from Work2");
        }

        private async Task Task3(String name, int num)
        {
            Console.WriteLine($"Hello from '{name}' {num}");
        }


        /* Повернення результату - або void, або Task/Task<TResult>
         * !!! void НЕ ОЧІКУЮТЬСЯ навіть якщо запускається через Task.Run()
         * Task<TResult> - "обгортка" над результатом, яка дозволяє його
         * очікувати. Тобто повертається деякий об'єкт, який згодом
         * перейде у стан "визначеності". У цьому стані буде доступний
         * результат відповідного типу.
         */
        private async Task<String> Task4(String name, int num)
        {
            return $"Hello from '{name}' {num}";   // return new Task(...)
        }

        private async Task<String> Task5(String input)
        {
            return input + " Task 5 addon";
        }
    }
}

/* Асинхронність. ч.2: Багатозадачність
 * Багатозадачність - використання об'єктів рівня мови програмування
 * (не операційної системи) для створення асинхронності.
 * Рекомендований підхід для більшості завдань
 * 
 * Задачу можна запустити кількома способами:
 *  Task.Run(Action)
 *  new Task(Action).Start()
 * При завершенні основної програми завершуються усі задачі.
 * Ті, що не встигли завершитись - скасовуються.
 * 
 * Реальне (приховане) виконання задач здійснюється спеціальним
 * "виконавцем" (ThreadPool): запуск задачі = постановка у чергу
 * до виконавця. Ємність керується ОС і варіюється на різних ПК (12-16-20-24).
 * ThreadPool скасовується після зупинки програми і всі задачі в ньому
 * зупиняються.
 * !!! Відмінність у запуску методів полягає у їх сигнатурі:
 *  звичайні - запускаються у головному потоці
 *  async - у ThreadPool
 *  
 *  
 *  ---- Main -- Demo -- cw(MainFinish) ---|
 *  
 *  ---- Main - cw(MainFinish) -|
 *            \                 |
 *             \     await      |
 *               Demo ---- task1  
 *                              \  \  \
 *                               \  \  work2 
 *                                \  task2
 *                                 work1
 * 
 *  ---- Main -cw(MainFinish)-|
 *            \               |
 *             \     await    |  await
 *               Demo ---- task1 ---- work1 
 *                                     \  \
 *                                      \  work2 
 *                                       task2
 *                                                    
 * Головні відмінності багатозадачності від багатопоточності
 * - пріоритети виконання: потоки мають однаковий пріоритет (з головним),
 *     задачі - фоновий. Це призводить до того, що з завершенням головного потоку
 *     = задачі скасовуються, навіть якщо не були завершені
 *     = потоки продовжують роботу, перебираючи на себе мітку "головний потік"
 * - до задач можна передавати довільну кількість аргументів довільних типів
 *    до потоків - тільки один узагальнений (object?) аргумент
 * - задачі можуть повертати значення (у вигляді об'єктів-задач)
 *    потоки - тільки змінювати "глобальні" ресурси.
 *    
 *    
 * Ниткове програмування / Continuations
 * Використовується для паралельних задач, кожна з яких вимагає послідовних дій.
 * Наприклад:
 * Підключення до БД (зчитуємо db.config -- підключаємось до DB -- синхронізуємо контекст)
 * Оновлюємо акції (підключаємось до АРІ акцій -- одержуємо JSON -- декодуємо, виводимо)
 * Автентифікуємо користувача (підключаємось до OAUTH -- обмінюємось даними)
 * 
 * LoadConfig() -- return config ->|
 *             v-<-----------------|
 *  .then(ConnectDB) -- return connection ->|
 *          v---<---------------------------|
 *  .then(SyncDB)
 *  
 * 
 */
/* Д.З. 1. Згенерувати випадкову перестановку цифр "0123456789" за
 * допомогою багатоЗАДАЧНОЇ програми:
 * запускається 10 задач, кожна з яких додає до спільного рядка одну цифру
 * а також виводить проміжний результат
 * У кінці роботи потоків виводиться підсумковий результат.
 * 
 * Очікуваний приклад:
 * thread 7:  "7"
 * thread 3:  "73"
 * thread 5:  "735"
 * ....
 * ---------------
 * total: "7350124689"
 * 
 * 2. Вирішити завдання з розрахунку річної інфляції засобами багатозадачності.
 */