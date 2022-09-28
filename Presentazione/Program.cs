/*
 1) il generatore pazienti crea i pazienti e li mette in una coda di attesa generica
 2) il triage prende i pazienti dall'attesa generica e gli assegna un codice (rosso, giallo o verde)
 3) ciascun medico prende pazienti dalle code corrispondenti ai vari codici e gli prescrive una ricetta/ricovero: i pazienti sono rimessi in una coda per le ricette
 4) il triage prende i pazienti dall'attesa post visita e gli stampa la ricetta/ricovera
 */

using System.Collections.Concurrent;

namespace presentazione
{
    internal class Presentazione
    {
        static object stampaSincronizzata = new object();
        public enum PossibileEsito{
            Ricovero = 0,
            Ricetta = 1,
            TuttoOk = 2
        }

        public enum CodiceOperativo{
            rosso = 0,
            giallo = 1,
            verde = 2
        }

        class Paziente {
            private static int idcorrente = 0;
            public int Id { get; set; }
            private CodiceOperativo cod;
            public CodiceOperativo Cod { get => cod; set => cod = value; } //0 = rosso, 1 = giallo, 2 = verde
            public PossibileEsito Esito { get; set; }

            public Paziente(){
                Id = idcorrente++;
            }
        }
        class SalaAttesa {
            public ConcurrentQueue<Paziente> AttesaGenerica { get; set; }
            public ConcurrentQueue<Paziente> CodiceRosso { get; set; }
            public ConcurrentQueue<Paziente> CodiceGiallo { get; set; }
            public ConcurrentQueue<Paziente> CodiceVerde { get; set; }
            public ConcurrentQueue<Paziente> VisitaFinita { get; set; }
            public object triageAttesa;
            public object medicoAttesa;
            public SalaAttesa(){
                AttesaGenerica = new ConcurrentQueue<Paziente>();
                CodiceRosso = new ConcurrentQueue<Paziente>();
                CodiceGiallo = new ConcurrentQueue<Paziente>();
                CodiceVerde = new ConcurrentQueue<Paziente>();
                VisitaFinita = new ConcurrentQueue<Paziente>();
                triageAttesa = new object();
                medicoAttesa = new object();
            }
        }
        class Medico
        {
            private static int idcorrente = 0;
            public int Id { get; set; }
            public Medico(SalaAttesa s){
                lavoroMedico = new Thread(medicoRun);
                Sa = s;
                Id = idcorrente++;
            }
            public SalaAttesa Sa { get; set; }
            public Thread lavoroMedico;
            private void medicoRun()
            {
                while (true){
                    if (Sa.CodiceRosso.IsEmpty && Sa.CodiceGiallo.IsEmpty && Sa.CodiceVerde.IsEmpty){
                        lock (stampaSincronizzata){
                            Console.WriteLine("il medico {0} si mette in attesa..", Id);   
                        }
                        lock (Sa.medicoAttesa){
                            Monitor.Wait(Sa.medicoAttesa);
                        }
                    }
                    lock (stampaSincronizzata){
                        Console.WriteLine("il medico {0} si mette all'opera", Id);
                    }
                    Paziente p;
                    if (Sa.CodiceRosso.TryDequeue(out p)){
                        lock (stampaSincronizzata){
                            Console.WriteLine("il medico {0} visita il paziente {1}; rimangono {2} pazienti in codice {3}"
                                , Id, p.Id, Sa.CodiceRosso.Count, p.Cod.ToString());
                        }
                        p.Esito = PossibileEsito.Ricovero;
                        Sa.VisitaFinita.Enqueue(p);
                    }
                    else if (Sa.CodiceGiallo.TryDequeue(out p)){
                        lock (stampaSincronizzata){
                            Console.WriteLine("il medico {0} visita il paziente {1}; rimangono {2} pazienti in codice {3}"
                                , Id, p.Id, Sa.CodiceGiallo.Count, p.Cod.ToString());
                        }
                        p.Esito = PossibileEsito.Ricetta;
                        Sa.VisitaFinita.Enqueue(p);
                    }
                    else if (Sa.CodiceVerde.TryDequeue(out p)){
                        lock (stampaSincronizzata){
                            Console.WriteLine("il medico {0} visita il paziente {1}; rimangono {2} pazienti in codice {3}"
                                , Id, p.Id, Sa.CodiceVerde.Count, p.Cod.ToString());
                        }
                        p.Esito = PossibileEsito.TuttoOk;
                        Sa.VisitaFinita.Enqueue(p);
                    }
                }
            }
        }

        class Triage{
            public SalaAttesa Sa { get; set; }
            public Thread lavoroTriage;
            public Triage(SalaAttesa s) {
                Sa = s;
                lavoroTriage = new Thread(triageRun);
            }
            public void triageRun(){
                while (true){
                    if (Sa.AttesaGenerica.IsEmpty && Sa.VisitaFinita.IsEmpty)
                        lock (Sa.triageAttesa){
                            lock (stampaSincronizzata)
                                Console.WriteLine("il triage attende");
                            Monitor.Wait(Sa.triageAttesa);
                        }
                    Paziente p;
                    Random rnd = new Random();
                    if (Sa.AttesaGenerica.TryDequeue(out p)){
                        p.Cod = (CodiceOperativo)rnd.Next(0, 3);
                        lock (stampaSincronizzata){
                            Console.WriteLine("il paziente {0} è un codice {1}", p.Id, p.Cod.ToString());
                        }
                        switch (p.Cod){
                            case CodiceOperativo.rosso:
                                lock (Sa.medicoAttesa){
                                    Sa.CodiceRosso.Enqueue(p);
                                    Monitor.PulseAll(Sa.medicoAttesa);
                                }
                                break;
                            case CodiceOperativo.giallo:
                                lock (Sa.medicoAttesa){
                                    Sa.CodiceGiallo.Enqueue(p);
                                    Monitor.PulseAll(Sa.medicoAttesa);
                                }
                                break;
                            case CodiceOperativo.verde:
                                lock (Sa.medicoAttesa){
                                    Sa.CodiceVerde.Enqueue(p);
                                    Monitor.PulseAll(Sa.medicoAttesa);
                                }
                                break;
                        }
                    }
                    if (Sa.VisitaFinita.TryDequeue(out p)){
                        lock (stampaSincronizzata){
                            Console.WriteLine("il paziente {0} viene congedato con {1}", p.Id, p.Esito.ToString());
                        }
                    }
                }
            }
        }


        static void Main(string[] args)
        {
            SalaAttesa sa = new SalaAttesa();
            Thread generatorePazienti = new Thread(() =>
            {
                while (true){
                    Paziente p = new Paziente();
                    sa.AttesaGenerica.Enqueue(p);
                    lock (stampaSincronizzata){
                        Console.WriteLine("Arriva il paziente {0}: ci sono ora {1} persone in attesa", p.Id, sa.AttesaGenerica.Count());
                    }
                    lock (sa.triageAttesa){
                        Monitor.PulseAll(sa.triageAttesa);
                    }
                    //Thread.Sleep(1000);
                }
            });
            generatorePazienti.Start();
            Triage t = new Triage(sa);
            //Triage t2 = new Triage(sa);
            Medico m1 = new Medico(sa);
            //Medico m2 = new Medico(sa);
            t.lavoroTriage.Start();
            m1.lavoroMedico.Start();
            //m2.lavoroMedico.Start();
        }

        
    }

}
