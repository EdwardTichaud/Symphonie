using NUnit.Framework;
using UnityEngine;

/// <summary>
///     Batterie de tests de régression pour <see cref="AltitudeOverrideStatus"/>.
///     On vérifie que l'état interne reste cohérent lorsqu'on bascule successivement
///     entre un ancrage au sol et une suspension dans les airs, et que la décrémentation
///     par tours fonctionne sans jamais produire de valeurs négatives.
/// </summary>
public class AltitudeOverrideStatusTests
{
    [Test]
    public void AnchorThenSuspend_Should_ClearGroundOverride_And_ReportActiveAirOverride()
    {
        // On instancie un GameObject neutre afin de simuler l'entité de jeu qui hébergera le composant.
        var go = new GameObject("AltitudeOverrideStatusTests_GO");
        try
        {
            // Ajout du composant sous test : cela reproduit le comportement réel dans la scène Unity.
            var status = go.AddComponent<AltitudeOverrideStatus>();

            // On applique d'abord un ancrage au sol pour deux tours.
            status.AnchorToGround(2);
            Assert.IsTrue(status.HasActiveOverride, "L'ancrage au sol devrait être actif après l'appel initial.");
            Assert.IsTrue(status.IsForcedGrounded, "L'état 'au sol' doit être actif tant que le compteur est positif.");
            Assert.IsFalse(status.IsSuspendedInAir, "Aucune suspension aérienne ne doit être présente après AnchorToGround.");

            // On applique ensuite une suspension aérienne pour trois tours.
            status.SuspendInAir(3);

            // L'appel précédent doit avoir annulé l'ancrage pour éviter les incohérences.
            Assert.IsTrue(status.HasActiveOverride, "Une suspension dans les airs devrait être signalée comme override actif.");
            Assert.IsFalse(status.IsForcedGrounded, "La suspension en l'air annule l'ancrage au sol.");
            Assert.IsTrue(status.IsSuspendedInAir, "L'état 'en l'air' doit être actif après SuspendInAir.");
        }
        finally
        {
            // Nettoyage afin d'éviter la pollution de la scène Unity lors de l'exécution des tests EditMode.
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TickTurn_Should_DecrementCounters_WithoutGoingNegative()
    {
        var go = new GameObject("AltitudeOverrideStatusTests_Tick_GO");
        try
        {
            var status = go.AddComponent<AltitudeOverrideStatus>();

            // Mise en place d'une suspension aérienne pour deux tours.
            status.SuspendInAir(2);

            // Premier Tick : le compteur doit passer de 2 à 1.
            Assert.IsTrue(status.TickTurn(), "Après un tour, un override doit toujours être actif.");
            // Afin de documenter le comportement : HasActiveOverride reflète l'état de la suspension.
            Assert.IsTrue(status.HasActiveOverride, "Le compteur ne doit pas passer à zéro avant deux appels à TickTurn.");

            // Second Tick : le compteur doit atteindre zéro et l'override expirer.
            Assert.IsFalse(status.TickTurn(), "Après deux tours, la suspension doit s'achever.");
            Assert.IsFalse(status.HasActiveOverride, "Après expiration, aucun override ne doit rester actif.");
            Assert.IsFalse(status.IsSuspendedInAir, "La suspension doit être complètement levée une fois le compteur à zéro.");

            // Troisième Tick : appel supplémentaire pour vérifier l'absence de valeurs négatives ou de réactivation.
            Assert.IsFalse(status.TickTurn(), "Sans override actif, TickTurn doit renvoyer faux.");
            Assert.IsFalse(status.HasActiveOverride, "Aucun override ne doit être recréé accidentellement.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
